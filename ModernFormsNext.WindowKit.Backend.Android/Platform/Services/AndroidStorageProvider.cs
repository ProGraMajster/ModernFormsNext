using Android.Content;
using Android.Provider;
using Android.Webkit;
using ModernFormsNext.WindowKit.Platform.Storage;
using ModernFormsNext.WindowKit.Platform.Storage.FileIO;
using NativeUri = Android.Net.Uri;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

internal sealed class AndroidStorageProvider(Context context, AndroidActivityResultCoordinator requests) : IStorageProvider
{
    private Context Context { get; } = context.ApplicationContext!;
    public bool CanOpen => Available(Intent.ActionOpenDocument!);
    public bool CanSave => Available(Intent.ActionCreateDocument!);
    public bool CanPickFolder => Available(Intent.ActionOpenDocumentTree!);
    private bool Available(string action)
    {
        requests.VerifyAccess();
        if (requests.Availability != ModernFormsNext.WindowKit.Platform.Services.PlatformServiceStatus.Success) return false;
        using var intent = new Intent(action);
        if (action != Intent.ActionOpenDocumentTree) { intent.AddCategory(Intent.CategoryOpenable!); intent.SetType("*/*"); }
        return intent.ResolveActivity(Context.PackageManager!) is not null;
    }
    public async Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        requests.VerifyAccess();
        using var intent = Picker(Intent.ActionOpenDocument!, options, options.FileTypeFilter);
        intent.PutExtra(Intent.ExtraAllowMultiple, options.AllowMultiple);
        var result = await requests.Start(intent, options.CancellationToken).ConfigureAwait(false);
        if (!result.Accepted) return [];
        var uris = options.AllowMultiple ? result.Uris : result.Uris.Take(1);
        var files = new List<IStorageFile>();
        foreach (var uri in uris)
            files.Add((IStorageFile)await CreateItem(uri, false, (ActivityFlags)result.Flags, selected: true).ConfigureAwait(false));
        return files.AsReadOnly();
    }
    public async Task<IStorageFile?> SaveFilePickerAsync(FilePickerSaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        requests.VerifyAccess();
        using var intent = Picker(Intent.ActionCreateDocument!, options, options.FileTypeChoices);
        intent.PutExtra(Intent.ExtraTitle, AndroidServicePlans.SuggestedName(options.SuggestedFileName, options.DefaultExtension));
        var result = await requests.Start(intent, options.CancellationToken).ConfigureAwait(false);
        return result.Accepted && result.Uris.Count > 0
            ? (IStorageFile)await CreateItem(result.Uris[0], false, (ActivityFlags)result.Flags, selected: true).ConfigureAwait(false) : null;
    }
    public async Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        requests.VerifyAccess();
        if (options.AllowMultiple) throw new NotSupportedException("Android SAF selects one folder tree per request.");
        using var intent = Picker(Intent.ActionOpenDocumentTree!, options, null);
        var result = await requests.Start(intent, options.CancellationToken).ConfigureAwait(false);
        return result.Accepted && result.Uris.Count > 0
            ? [(IStorageFolder)await CreateItem(result.Uris[0], true, (ActivityFlags)result.Flags, selected: true).ConfigureAwait(false)] : [];
    }
    private static Intent Picker(string action, PickerOptions options, IReadOnlyList<FilePickerFileType>? filters)
    {
        var intent = new Intent(action);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission);
        if (action == Intent.ActionOpenDocumentTree) intent.AddFlags(ActivityFlags.GrantPrefixUriPermission);
        else
        {
            intent.AddCategory(Intent.CategoryOpenable!);
            var mime = AndroidServicePlans.MimeTypes(filters, extension => MimeTypeMap.Singleton?.GetMimeTypeFromExtension(extension));
            if (action == Intent.ActionCreateDocument)
            {
                // ACTION_CREATE_DOCUMENT accepts one MIME, not desktop file-type choices.
                var save = (FilePickerSaveOptions)options;
                var inferred = AndroidServicePlans.MimeForExtension(save.DefaultExtension ??
                    System.IO.Path.GetExtension(save.SuggestedFileName ?? ""));
                intent.SetType(mime[0] == "*/*" ? inferred ?? "application/octet-stream" : mime[0]);
            }
            else
            {
                intent.SetType(mime.Length == 1 ? mime[0] : "*/*");
                if (mime.Length > 1) intent.PutExtra(Intent.ExtraMimeTypes, mime);
            }
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && options.SuggestedStartLocation?.Path is { IsAbsoluteUri: true } start)
        {
            using var uri = NativeUri.Parse(start.AbsoluteUri);
            intent.PutExtra(DocumentsContract.ExtraInitialUri, uri);
        }
        return intent;
    }
    private Task<IStorageItem> CreateItem(Uri uri, bool folder, ActivityFlags flags, bool selected) => Task.Run<IStorageItem>(() =>
    {
        using var original = NativeUri.Parse(uri.AbsoluteUri)!;
        Uri? tree = folder && IsTree(original) ? uri : null;
        using var document = tree is not null ?
            DocumentsContract.BuildDocumentUriUsingTree(original, DocumentsContract.GetTreeDocumentId(original))! :
            NativeUri.Parse(uri.AbsoluteUri)!;
        AndroidStorageItem.Metadata? metadata;
        try { metadata = AndroidStorageItem.ReadMetadata(Context.ContentResolver!, document); }
        catch (Java.Lang.IllegalArgumentException) when (selected) { metadata = null; }
        if (!selected && metadata is null) throw new Java.IO.FileNotFoundException("The provider cannot resolve this document.");
        if (folder)
        {
            if (tree is null) throw new NotSupportedException("A folder requires a SAF tree URI.");
            return new AndroidStorageFolder(Context, new Uri(document.ToString()!), metadata?.Name ?? "", tree, flags, uri);
        }
        return new AndroidStorageFile(Context, uri, metadata?.Name ?? "", flags);
    });
    public async Task<IStorageFile?> TryGetFileFromPathAsync(Uri filePath)
        => await TryGet(filePath, false).ConfigureAwait(false) as IStorageFile;
    public async Task<IStorageFolder?> TryGetFolderFromPathAsync(Uri folderPath)
        => await TryGet(folderPath, true).ConfigureAwait(false) as IStorageFolder;
    private async Task<IStorageItem?> TryGet(Uri uri, bool folder)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri) return null;
        if (uri.IsFile)
            return await Task.Run<IStorageItem?>(() => folder ?
                Directory.Exists(uri.LocalPath) ? new BclStorageFolder(new DirectoryInfo(uri.LocalPath)) : null :
                File.Exists(uri.LocalPath) ? new BclStorageFile(new FileInfo(uri.LocalPath)) : null).ConfigureAwait(false);
        if (uri.Scheme != "content" || string.IsNullOrEmpty(uri.Host)) return null;
        try
        {
            return await Task.Run(async () =>
            {
                using var native = NativeUri.Parse(uri.AbsoluteUri)!;
                if (folder && !IsTree(native)) return null;
                if (!folder)
                {
                    var metadata = AndroidStorageItem.ReadMetadata(Context.ContentResolver!, native);
                    if (metadata is null || metadata.Mime == DocumentsContract.Document.MimeTypeDir) return null;
                }
                return await CreateItem(uri, folder, 0, false).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (Exception e) when (e is Java.Lang.SecurityException or Java.IO.FileNotFoundException or Java.Lang.IllegalArgumentException)
        { return null; }
    }
    public async Task<IStorageBookmarkFile?> OpenFileBookmarkAsync(string bookmark)
        => await OpenBookmark(bookmark, false).ConfigureAwait(false) as IStorageBookmarkFile;
    public async Task<IStorageBookmarkFolder?> OpenFolderBookmarkAsync(string bookmark)
        => await OpenBookmark(bookmark, true).ConfigureAwait(false) as IStorageBookmarkFolder;
    private async Task<IStorageItem?> OpenBookmark(string bookmark, bool folder)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        string prefix = "mfn-saf-v1:" + (folder ? "d:" : "f:");
        if (!bookmark.StartsWith(prefix, StringComparison.Ordinal)) return null;
        Uri uri;
        try
        {
            string value = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(bookmark[prefix.Length..]));
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri!) || uri.Scheme != "content") return null;
        }
        catch (FormatException) { return null; }
        return await Task.Run(async () =>
        {
            var grant = Context.ContentResolver!.PersistedUriPermissions?.FirstOrDefault(p => p.Uri?.ToString() == uri.AbsoluteUri && p.IsReadPermission);
            if (grant is null || await TryGet(uri, folder).ConfigureAwait(false) is not { } probe) return null;
            probe.Dispose();
            return await CreateItem(uri, folder, ActivityFlags.GrantPersistableUriPermission | ActivityFlags.GrantReadUriPermission |
                (grant.IsWritePermission ? ActivityFlags.GrantWriteUriPermission : 0), false).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
    private static bool IsTree(NativeUri uri)
        => OperatingSystem.IsAndroidVersionAtLeast(24) ? DocumentsContract.IsTreeUri(uri)
            : uri.PathSegments is { Count: >= 2 } segments && segments[0] == "tree";

    public Task<IStorageFolder?> TryGetWellKnownFolderAsync(WellKnownFolder wellKnownFolder)
    {
        if (!Enum.IsDefined(wellKnownFolder)) throw new ArgumentOutOfRangeException(nameof(wellKnownFolder));
        // Shared public collections have no universally accessible raw path on scoped-storage
        // Android. Do not misleadingly map them to unrelated app-private directories.
        return Task.FromResult<IStorageFolder?>(null);
    }
}
