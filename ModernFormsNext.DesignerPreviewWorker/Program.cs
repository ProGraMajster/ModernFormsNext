using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using ModernFormsNext.Designer.ExecutablePreview;
using SkiaSharp;
using FilePath = System.IO.Path;

namespace ModernFormsNext.DesignerPreviewWorker;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Accidental direct invocation must never load a project assembly. The trusted
        // launcher also verifies the exact package SID, capability count, LPAC access and job.
        if (args.Length != 6 || !IsAppContainer() ||
            !Guid.TryParseExact(args[2], "N", out Guid session) || session == Guid.Empty ||
            !long.TryParse(args[3], CultureInfo.InvariantCulture, out long request) || request <= 0 ||
            !long.TryParse(args[4], CultureInfo.InvariantCulture, out long revision) || revision < 0 ||
            !int.TryParse(args[5], CultureInfo.InvariantCulture, out int node) || node < 0)
            return 2;

        var key = new PreviewKey(session, request, revision, node);
        try
        {
            using var input = new AnonymousPipeClientStream(PipeDirection.In, args[0]);
            using var output = new AnonymousPipeClientStream(PipeDirection.Out, args[1]);
            if (node == 0)
                return SandboxProbe.Run(output, key);

            Write(output, key, PreviewMessage.Ready, []);
            var packet = PreviewWire.ReadAsync(input, key, PreviewMessage.Request, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            PreviewWire.RequireEndOfStreamAsync(input, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            Render(PreviewWire.Decode(packet.Payload), key, output);
            return 0;
        }
        catch
        {
            // Project exception messages and paths never go to stderr or the host.
            return 3;
        }
    }

    private static void Render(PreviewRequest request, PreviewKey key, Stream output)
    {
        PreviewPhase phase = PreviewPhase.Load;
        Control? control = null;
        ProjectLoadContext? context = null;
        byte[]? pixels = null;
        byte[]? failure = null;

        void Begin(PreviewPhase next)
        {
            phase = next;
            Write(output, key, PreviewMessage.Phase, [(byte)next]);
        }

        try
        {
            Begin(PreviewPhase.Load);
            context = new ProjectLoadContext(request.Digest);
            Type type = context.FindControl(request.Type);
            Begin(PreviewPhase.Construct);
            // The default CLR constructor runs before Site is available.
            control = (Control)Activator.CreateInstance(type)!;
            Begin(PreviewPhase.Properties);
            control.Site = new PreviewSite(control);
            foreach (var property in request.Properties)
                SetProperty(control, type, property);
            Begin(PreviewPhase.Layout);
            control.Size = new Size(Math.Max(1, (int)Math.Round(request.Width / request.Scale)),
                Math.Max(1, (int)Math.Round(request.Height / request.Scale)));
            control.PerformLayout();
            Begin(PreviewPhase.Paint);
            var info = new SKImageInfo(request.Width, request.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(info);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.Scale((float)request.Scale);
            var paint = new PaintEventArgs(info, canvas, 1);
            control.RaisePaintBackground(paint);
            control.RaisePaint(paint);
            canvas.Flush();
            pixels = new byte[info.BytesSize];
            Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        }
        catch (Exception error)
        {
            failure = GetFailure(phase, error, context);
        }
        finally
        {
            if (control is not null)
            {
                try
                {
                    // Retain the first failure if cleanup also faults.
                    Begin(PreviewPhase.Dispose);
                    control.Dispose();
                }
                catch (Exception error)
                {
                    failure ??= GetFailure(PreviewPhase.Dispose, error, context);
                }
            }
        }
        if (failure is not null)
            Write(output, key, PreviewMessage.Failure, failure);
        else
            Write(output, key, PreviewMessage.Result, pixels!);
    }

    private static byte[] GetFailure(PreviewPhase phase, Exception error, ProjectLoadContext? context)
    {
        while (error is TargetInvocationException or TypeInitializationException && error.InnerException is { } cause)
            error = cause;
        // The host resolves a bounded method token against its detached, approved metadata. Text
        // from thrown exceptions, stack formatting and source paths stays inside the worker.
        byte[] payload = [(byte)phase, 0, 0, 0, 0, 0];
        if (context is not null)
        {
            var stack = new StackTrace(error, fNeedFileInfo: false);
            for (int index = 0; index < Math.Min(stack.FrameCount, 24); index++)
            {
                var method = stack.GetFrame(index)?.GetMethod();
                if (method is null || !context.TryGetIndex(method.Module.Assembly, out byte assembly))
                    continue;
                try
                {
                    payload[1] = assembly;
                    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(2), method.MetadataToken);
                }
                catch (InvalidOperationException) { payload[1] = 0; }
                break;
            }
        }
        return payload;
    }

    private static void SetProperty(Control control, Type controlType, PreviewProperty property)
    {
        var target = controlType.GetProperty(property.Name, BindingFlags.Instance | BindingFlags.Public);
        if (target?.SetMethod is not { IsPublic: true } || target.GetIndexParameters().Length != 0)
            throw new NotSupportedException();
        var value = property.Value;
        Type type = Nullable.GetUnderlyingType(target.PropertyType) ?? target.PropertyType;
        object? converted = value.ValueKind switch
        {
            JsonValueKind.Null when !target.PropertyType.IsValueType || Nullable.GetUnderlyingType(target.PropertyType) is not null => null,
            JsonValueKind.String when type == typeof(string) => value.GetString(),
            JsonValueKind.String when type.IsEnum => Enum.Parse(type, value.GetString()!, ignoreCase: false),
            JsonValueKind.True or JsonValueKind.False when type == typeof(bool) => value.GetBoolean(),
            JsonValueKind.Number when type == typeof(int) => value.GetInt32(),
            JsonValueKind.Number when type == typeof(double) => value.GetDouble(),
            JsonValueKind.Number when type == typeof(float) && float.IsFinite(value.GetSingle()) => value.GetSingle(),
            _ => throw new NotSupportedException()
        };
        target.SetValue(control, converted);
    }

    internal static void Write(Stream output, PreviewKey key, PreviewMessage message, byte[] bytes)
        => PreviewWire.WriteAsync(output, key, message, bytes, CancellationToken.None).AsTask().GetAwaiter().GetResult();

    private static bool IsAppContainer()
    {
        if (!OpenProcessToken((nint)(-1), 8, out nint token))
            return false;
        try { return GetTokenInformation(token, 29, out int value, sizeof(int), out _) && value != 0; }
        finally { CloseHandle(token); }
    }

    [DllImport("advapi32.dll")]
    private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll")]
    private static extern bool GetTokenInformation(nint token, int kind, out int value, int size, out int required);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);

    private sealed record PreviewSite(IComponent Component) : ISite
    {
        public IContainer? Container => null;
        public bool DesignMode => true;
        public string? Name { get; set; }
        public object? GetService(Type serviceType) => null;
    }

    private sealed class ProjectLoadContext : AssemblyLoadContext
    {
        private readonly Dictionary<string, (string Path, byte Index)> files = new(StringComparer.OrdinalIgnoreCase);

        internal ProjectLoadContext(string digest)
        {
            string directory = FilePath.Combine(AppContext.BaseDirectory, "artifacts");
            var paths = Directory.GetFiles(directory, "*.dll").OrderBy(FilePath.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
            if (paths.Length is < 1 or > PreviewManifest.MaxFiles)
                throw new InvalidDataException();
            var hashes = new List<(string Name, string Hash)>();
            long total = 0;
            foreach (var path in paths)
            {
                string name = FilePath.GetFileNameWithoutExtension(path);
                using var file = File.OpenRead(path);
                if (!PreviewManifest.IsAssemblyName(name) || file.Length is <= 0 or > PreviewManifest.MaxFileBytes ||
                    (total += file.Length) > PreviewManifest.MaxTotalBytes)
                    throw new InvalidDataException();
                hashes.Add((name, Convert.ToHexString(SHA256.HashData(file))));
                files.Add(name, (path, (byte)(files.Count + 1)));
            }
            if (!string.Equals(PreviewManifest.GetDigest(hashes), digest, StringComparison.Ordinal))
                throw new InvalidDataException();
        }

        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name is not null && files.TryGetValue(name.Name, out var item))
            {
                var loaded = LoadFromAssemblyPath(item.Path);
                if (!AssemblyName.ReferenceMatchesDefinition(name, loaded.GetName()) || name.Version != loaded.GetName().Version)
                    throw new FileLoadException();
                return loaded;
            }
            // The default context contains only the worker's trusted, private runtime and dependencies.
            var trusted = Default.LoadFromAssemblyName(name);
            if (trusted.GetName().Name == "ModernFormsNext" && name.Version != trusted.GetName().Version)
                throw new FileLoadException();
            return trusted;
        }

        protected override nint LoadUnmanagedDll(string name) => throw new DllNotFoundException();

        internal bool TryGetIndex(Assembly assembly, out byte index)
        {
            index = 0;
            if (GetLoadContext(assembly) != this || !files.TryGetValue(assembly.GetName().Name!, out var file))
                return false;
            index = file.Index;
            return true;
        }

        internal Type FindControl(string typeName)
        {
            Type? result = null;
            foreach (var item in files.Values)
            {
                var type = LoadFromAssemblyPath(item.Path).GetType(typeName, throwOnError: false);
                if (type is null)
                    continue;
                if (result is not null || GetLoadContext(type.Assembly) != this || !type.IsVisible ||
                    type.IsAbstract || type.ContainsGenericParameters || !typeof(Control).IsAssignableFrom(type) ||
                    typeof(Form).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) is null)
                    throw new NotSupportedException();
                result = type;
            }
            return result ?? throw new TypeLoadException();
        }
    }
}
