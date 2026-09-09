using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using IOPath = System.IO.Path;

namespace ModernFormsNext.Automation.Windows;

/// <summary>Discovers explicitly enabled local bridges from restricted per-user metadata, without scanning named pipes.</summary>
public static class AutomationDiscovery
{
    /// <summary>Gets the deterministic per-user descriptor directory. Reading this property creates nothing.</summary>
    public static string DirectoryPath => IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ModernFormsNext", "Automation", "v1");

    /// <summary>Reads live descriptors and removes safely identified stale/orphan records.</summary>
    /// <param name="cancellationToken">Cancels between bounded file/process checks.</param>
    /// <returns>At most 1024 live instances, sorted by instance ID. An absent directory returns an empty array.</returns>
    /// <remarks>May run on any thread. Throws a safe UnsafeDiscovery error for an unsafe directory. Malformed files are ignored; no arbitrary paths are deleted.</remarks>
    public static Task<ImmutableArray<AutomationApplicationInfo>> DiscoverAsync(CancellationToken cancellationToken = default)
        => Task.Run(() => Discover(cancellationToken), cancellationToken);

    private static ImmutableArray<AutomationApplicationInfo> Discover(CancellationToken token)
    {
        if (!Directory.Exists(DirectoryPath)) return [];
        var user = PipeSecurityPolicy.CurrentUser(); ValidateDirectory(user);
        var result = ImmutableArray.CreateBuilder<AutomationApplicationInfo>();
        foreach (string path in Directory.EnumerateFiles(DirectoryPath, "*.json").Take(1024))
        {
            token.ThrowIfCancellationRequested();
            string instance = IOPath.GetFileNameWithoutExtension(path);
            if (!ValidInstance(instance)) continue;
            try
            {
                var descriptor = ReadDescriptor(instance, user);
                if (descriptor is null) continue;
                if (!IsLive(descriptor)) { Remove(instance); continue; }
                if (!File.Exists(AuthPath(instance))) continue;
                ValidateFile(AuthPath(instance), user);
                result.Add(descriptor);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or AutomationTransportException) { }
        }
        // Orphan secrets can result from a crash during publication. Do not remove a freshly
        // publishing server's file; the age guard is for orphan cleanup, not process identity.
        foreach (string path in Directory.EnumerateFiles(DirectoryPath, "*.auth").Take(1024))
        {
            token.ThrowIfCancellationRequested();
            string instance = IOPath.GetFileNameWithoutExtension(path);
            if (!ValidInstance(instance) || File.Exists(DescriptorPath(instance))) continue;
            try
            {
                ValidateFile(path, user);
                if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddMinutes(-2)) File.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or AutomationTransportException) { }
        }
        return result.OrderBy(x => x.InstanceId, StringComparer.Ordinal).ToImmutableArray();
    }

    internal static void Publish(AutomationApplicationInfo info, byte[] secret, SecurityIdentifier user)
    {
        EnsureDirectory(user);
        WriteRestricted(AuthPath(info.InstanceId), secret, user);
        try { WriteRestricted(DescriptorPath(info.InstanceId), JsonSerializer.SerializeToUtf8Bytes(info, Protocol.Json), user); }
        catch { Remove(info.InstanceId); throw; }
    }

    internal static AutomationApplicationInfo? ReadDescriptor(string instance, SecurityIdentifier user)
    {
        if (!ValidInstance(instance)) return null;
        string path = DescriptorPath(instance); ValidateFile(path, user);
        using var stream = File.OpenRead(path);
        if (stream.Length is <= 0 or > 4096) return null;
        var info = JsonSerializer.Deserialize<AutomationApplicationInfo>(stream, Protocol.Json);
        return info is not null && info.InstanceId == instance && ValidInstance(info.InstanceId)
            && info.ProcessId > 0 && info.EndpointName == Endpoint(info.ProcessId, instance)
            && info.ApplicationName is { Length: > 0 and <= 128 } && !info.ApplicationName.Any(char.IsControl)
            && Protocol.ValidCapabilities(info.Capabilities)
            && long.TryParse(info.ProcessStartUtcTicks, NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) && ticks > 0
            ? info : null;
    }

    internal static byte[] ReadSecret(AutomationApplicationInfo info)
    {
        var user = PipeSecurityPolicy.CurrentUser(); ValidateDirectory(user);
        if (ReadDescriptor(info.InstanceId, user) != info || !IsLive(info))
            throw new AutomationTransportException(AutomationTransportError.ApplicationUnavailable);
        string path = AuthPath(info.InstanceId); ValidateFile(path, user);
        using var stream = File.OpenRead(path);
        if (stream.Length != 32) throw new AutomationTransportException(AutomationTransportError.AuthenticationFailed);
        byte[] secret = new byte[32]; stream.ReadExactly(secret); return secret;
    }

    internal static bool IsLive(AutomationApplicationInfo info)
    {
        try
        {
            using var process = Process.GetProcessById(info.ProcessId);
            return !process.HasExited && ProcessStart(process) == info.ProcessStartUtcTicks;
        }
        catch (Exception) { return false; }
    }

    internal static string ProcessStart(Process process) => process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
    internal static string Endpoint(int pid, string instance) => $"mfn-automation-{pid}-{instance}";
    internal static bool ValidInstance(string instance) => Guid.TryParseExact(instance, "N", out var id) && id.ToString("N") == instance;
    internal static string DescriptorPath(string instance) => IOPath.Combine(DirectoryPath, instance + ".json");
    internal static string AuthPath(string instance) => IOPath.Combine(DirectoryPath, instance + ".auth");

    internal static void Remove(string instance)
    {
        if (!ValidInstance(instance)) return;
        var user = PipeSecurityPolicy.CurrentUser();
        foreach (string path in new[] { DescriptorPath(instance), AuthPath(instance) })
        {
            try { ValidateDirectory(user); if (File.Exists(path)) { ValidateFile(path, user); File.Delete(path); } }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or AutomationTransportException) { }
        }
    }

    private static void EnsureDirectory(SecurityIdentifier user)
    {
        var directory = new DirectoryInfo(DirectoryPath);
        RejectReparseAncestors(directory);
        if (!directory.Exists)
        {
            Directory.CreateDirectory(directory.Parent!.FullName);
            RejectReparseAncestors(directory);
            var security = new DirectorySecurity(); security.SetOwner(user); security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            directory.Create(security);
        }
        ValidateDirectory(user);
    }

    internal static void ValidateDirectory(SecurityIdentifier user)
    {
        var directory = new DirectoryInfo(DirectoryPath); RejectReparseAncestors(directory);
        ValidateAcl(directory.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner), user);
    }

    internal static void ValidateFile(string path, SecurityIdentifier user)
    {
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException();
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0) throw new AutomationTransportException(AutomationTransportError.UnsafeDiscovery);
        ValidateAcl(file.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner), user);
    }

    private static void RejectReparseAncestors(DirectoryInfo? directory)
    {
        for (; directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new AutomationTransportException(AutomationTransportError.UnsafeDiscovery);
    }

    private static void ValidateAcl(FileSystemSecurity security, SecurityIdentifier user)
    {
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
        if (!security.AreAccessRulesProtected || security.GetOwner(typeof(SecurityIdentifier)) != user || rules.Count != 1
            || rules[0] is not FileSystemAccessRule rule || rule.IdentityReference != user
            || rule.AccessControlType != AccessControlType.Allow || rule.FileSystemRights != FileSystemRights.FullControl
            || rule.IsInherited || rule.PropagationFlags != PropagationFlags.None)
            throw new AutomationTransportException(AutomationTransportError.UnsafeDiscovery);
    }

    private static void WriteRestricted(string path, byte[] data, SecurityIdentifier user)
    {
        var security = new FileSecurity(); security.SetOwner(user); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));
        using var stream = new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None,
            4096, FileOptions.None, security);
        stream.Write(data); stream.Flush(true);
    }
}
