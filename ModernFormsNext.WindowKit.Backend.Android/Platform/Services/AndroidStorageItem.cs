using Android.Content;
using Android.Provider;
using ModernFormsNext.WindowKit.Platform.Storage;
using NativeUri = Android.Net.Uri;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

// Only application context survives. Content-provider IPC is explicitly moved off the Looper;
// streams are returned directly, never buffered into memory. Dispose does not revoke grants.
internal abstract class AndroidStorageItem : IStorageBookmarkItem
{
    protected readonly Context Context;
    protected ContentResolver Resolver => Context.ContentResolver!;
    protected readonly Uri? Tree;
    private readonly Uri grantUri;
    private readonly ActivityFlags grantFlags;
    private readonly AndroidStorageFolder? parent;
    private bool disposed;
    public string Name { get; }
    public Uri Path { get; }
    public bool CanBookmark => (grantFlags & ActivityFlags.GrantPersistableUriPermission) != 0;
    internal AndroidStorageItem(Context context, Uri uri, string name, Uri? tree,
        ActivityFlags flags = 0, Uri? grantUri = null, AndroidStorageFolder? parent = null)
    {
        Context = context.ApplicationContext!;
        Path = uri; Name = name; Tree = tree;
        grantFlags = flags; this.grantUri = grantUri ?? uri; this.parent = parent;
    }
    protected NativeUri Native() { Verify(); return NativeUri.Parse(Path.AbsoluteUri)!; }
    protected void Verify() => ObjectDisposedException.ThrowIf(disposed, this);
    public Task<StorageItemProperties> GetBasicPropertiesAsync() => Task.Run(() =>
    {
        using var uri = Native();
        return ReadMetadata(Resolver, uri)?.Properties ?? new StorageItemProperties();
    });
    public Task<string?> SaveBookmarkAsync() => Task.Run(() =>
    {
        Verify();
        if (!CanBookmark) return null;
        using var uri = NativeUri.Parse(grantUri.AbsoluteUri)!;
        var modes = grantFlags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        if (modes == 0) return null;
        try { Resolver.TakePersistableUriPermission(uri, modes); }
        catch (Java.Lang.SecurityException) { return null; }
        return "mfn-saf-v1:" + (this is IStorageFolder ? "d:" : "f:") +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(grantUri.AbsoluteUri));
    });
    public Task ReleaseBookmarkAsync() => Task.Run(() =>
    {
        Verify();
        using var uri = NativeUri.Parse(grantUri.AbsoluteUri)!;
        var grant = Resolver.PersistedUriPermissions?.FirstOrDefault(p => p.Uri?.ToString() == grantUri.AbsoluteUri);
        if (grant is null) return;
        var modes = (grant.IsReadPermission ? ActivityFlags.GrantReadUriPermission : 0) |
            (grant.IsWritePermission ? ActivityFlags.GrantWriteUriPermission : 0);
        Resolver.ReleasePersistableUriPermission(uri, modes);
    });
    public Task<IStorageFolder?> GetParentAsync()
    {
        Verify();
        // Returned wrappers have independent disposal, including a parent already held by
        // the caller. Never hand ownership of the original folder back through a child.
        return Task.FromResult<IStorageFolder?>(parent is null ? null :
            new AndroidStorageFolder(Context, parent.Path, parent.Name, parent.Tree!,
                parent.grantFlags, parent.grantUri, parent.parent));
    }
    public Task DeleteAsync() => Task.Run(() =>
    {
        using var uri = Native();
        if (!DocumentsContract.IsDocumentUri(Context, uri))
            throw new NotSupportedException("This URI is not a deletable document.");
        if (!DocumentsContract.DeleteDocument(Resolver, uri)) throw new IOException("The document provider refused deletion.");
    });
    public Task<IStorageItem?> MoveAsync(IStorageFolder destination)
    {
        Verify(); ArgumentNullException.ThrowIfNull(destination);
        throw new NotSupportedException("Moving documents between providers is not supported. Copy using streams explicitly.");
    }
    public void Dispose() => disposed = true;

    internal static Metadata? ReadMetadata(ContentResolver resolver, NativeUri uri)
    {
        // Some providers reject optional columns. Query their default projection and only use
        // columns actually returned; null dates/sizes and empty names are truthful.
        using var cursor = resolver.Query(uri, null, null, null, null);
        if (cursor?.MoveToFirst() != true) return null;
        string? Text(string column)
        {
            int index = cursor.GetColumnIndex(column);
            return index < 0 || cursor.IsNull(index) ? null : cursor.GetString(index);
        }
        long? Number(string column)
        {
            int index = cursor.GetColumnIndex(column);
            return index < 0 || cursor.IsNull(index) ? null : cursor.GetLong(index);
        }
        long? size = Number("_size"), modified = Number("last_modified");
        return new(Text("_display_name") ?? "", Text("mime_type"),
            AndroidStorageMetadata.Create(size, modified));
    }
    internal sealed record Metadata(string Name, string? Mime, StorageItemProperties Properties);
}

internal sealed class AndroidStorageFile(Context context, Uri uri, string name,
    ActivityFlags flags = 0, Uri? tree = null, Uri? grantUri = null, AndroidStorageFolder? parent = null)
    : AndroidStorageItem(context, uri, name, tree, flags, grantUri, parent), IStorageBookmarkFile
{
    public Task<Stream> OpenReadAsync() => Task.Run(() =>
    {
        using var native = Native();
        return Resolver.OpenInputStream(native) ?? throw new IOException("The provider did not return a readable stream.");
    });
    public Task<Stream> OpenWriteAsync() => Task.Run(() =>
    {
        using var native = Native();
        // rwt requests truncate semantics. Providers that cannot write must report failure;
        // do not silently copy, append or invent a local path.
        return Resolver.OpenOutputStream(native, "rwt") ?? throw new IOException("The provider did not return a writable stream.");
    });
}

internal sealed class AndroidStorageFolder(Context context, Uri uri, string name, Uri tree,
    ActivityFlags flags = 0, Uri? grantUri = null, AndroidStorageFolder? parent = null)
    : AndroidStorageItem(context, uri, name, tree, flags, grantUri, parent), IStorageBookmarkFolder
{
    public async IAsyncEnumerable<IStorageItem> GetItemsAsync()
    {
        // Metadata enumeration occurs on a worker and returns only portable wrappers.
        // This bounds native cursor lifetime to the operation, never the async consumer.
        var items = await Task.Run(() =>
        {
            using var native = Native();
            using var treeUri = NativeUri.Parse(Tree!.AbsoluteUri)!;
            using var children = DocumentsContract.BuildChildDocumentsUriUsingTree(treeUri, DocumentsContract.GetDocumentId(native))!;
            using var cursor = Resolver.Query(children, ["document_id", "_display_name", "mime_type"], null, null, null);
            var result = new List<IStorageItem>();
            while (cursor?.MoveToNext() == true)
            {
                using var child = DocumentsContract.BuildDocumentUriUsingTree(treeUri, cursor.GetString(0))!;
                var childUri = new Uri(child.ToString()!);
                result.Add(cursor.GetString(2) == DocumentsContract.Document.MimeTypeDir
                    ? new AndroidStorageFolder(Context, childUri, cursor.GetString(1) ?? "", Tree, parent: this)
                    : new AndroidStorageFile(Context, childUri, cursor.GetString(1) ?? "", tree: Tree, parent: this));
            }
            return result;
        }).ConfigureAwait(false);
        foreach (var item in items) yield return item;
    }
    public async Task<IStorageFile?> CreateFileAsync(string name)
        => await Create(name, false).ConfigureAwait(false) as IStorageFile;
    public async Task<IStorageFolder?> CreateFolderAsync(string name)
        => await Create(name, true).ConfigureAwait(false) as IStorageFolder;
    private Task<IStorageItem?> Create(string name, bool folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.IndexOfAny(['/', '\\']) >= 0) throw new ArgumentException("A display name, not a path, is required.", nameof(name));
        return Task.Run<IStorageItem?>(() =>
        {
            using var native = Native();
            var mime = folder ? DocumentsContract.Document.MimeTypeDir :
                AndroidServicePlans.MimeForExtension(System.IO.Path.GetExtension(name)) ?? "application/octet-stream";
            using var child = DocumentsContract.CreateDocument(Resolver, native, mime, name);
            if (child is null) return null;
            var metadata = ReadMetadata(Resolver, child);
            var uri = new Uri(child.ToString()!);
            return folder ? new AndroidStorageFolder(Context, uri, metadata?.Name ?? "", Tree!, parent: this) :
                new AndroidStorageFile(Context, uri, metadata?.Name ?? "", tree: Tree, parent: this);
        });
    }
}
