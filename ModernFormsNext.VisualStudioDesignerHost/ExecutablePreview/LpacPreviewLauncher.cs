using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using ModernFormsNext.Designer.ExecutablePreview;
using FilePath = System.IO.Path;

namespace ModernFormsNext.VisualStudioDesignerHost.ExecutablePreview;

// Windows-only broker. The project code is never loaded by this process.
internal sealed class LpacPreviewLauncher : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Startup
    {
        public int Size;
        public IntPtr Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
        public short Show, ReservedSize;
        public IntPtr Reserved2, Stdin, Stdout, Stderr, Attributes;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public IntPtr Process, Thread; public uint Id, ThreadId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Capabilities { public IntPtr Sid, List; public uint Count, Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    private struct JobLimits {
        public long ProcessTime, JobTime;
        public uint Flags;
        public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
        public uint Processes;
        public UIntPtr Affinity;
        public uint Priority, Scheduling;
        public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes;
        public UIntPtr ProcessMemory, JobMemory, PeakProcess, PeakJob;
    }
    [DllImport("userenv.dll", CharSet=CharSet.Unicode)]
    private static extern int CreateAppContainerProfile(string name, string display, string description, IntPtr caps, uint count, out IntPtr sid);
    [DllImport("userenv.dll", CharSet=CharSet.Unicode)]
    private static extern int DeleteAppContainerProfile(string name);
    [DllImport("advapi32.dll", SetLastError=true)]
    private static extern IntPtr FreeSid(IntPtr sid);
    [DllImport("advapi32.dll", SetLastError=true)]
    private static extern bool OpenProcessToken(IntPtr p, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError=true)]
    private static extern bool GetTokenInformation(IntPtr token, int info, IntPtr output, int size, out int length);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl, uint revision, out IntPtr descriptor, out uint size);
    [DllImport("advapi32.dll", SetLastError=true)]
    private static extern bool DuplicateToken(IntPtr token, int level, out IntPtr duplicate);
    [DllImport("advapi32.dll", SetLastError=true)]
    private static extern bool AccessCheck(IntPtr descriptor, IntPtr token, uint access, ref GenericMapping mapping, IntPtr privileges, ref uint size, out uint granted, out bool status);
    [StructLayout(LayoutKind.Sequential)]
    private struct GenericMapping { public uint Read, Write, Execute, All; }
    [DllImport("advapi32.dll", SetLastError=true)]
    private static extern bool EqualSid(IntPtr a, IntPtr b);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern IntPtr LocalFree(IntPtr pointer);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr p, int count, uint flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern void DeleteProcThreadAttributeList(IntPtr p);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr old, IntPtr oldSize);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern bool CreateProcess(string exe, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref Startup startup, out ProcessInfo p);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int type, ref JobLimits limits, uint size);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool IsProcessInJob(IntPtr process, SafeFileHandle job, out bool result);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint wait);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private static int TokenNumber(IntPtr token, int kind, IntPtr space)
    {
        Win32(GetTokenInformation(token, kind, space, 4, out _), "Token information " + kind);
        return Marshal.ReadInt32(space);
    }

    private static bool HasAccessThroughPackageGroup(IntPtr token, string packageGroup)
    {
        // World satisfies the ordinary token pass. Package matching supplies the restricted pass.
        string sddl = "O:SYG:SYD:(A;;0x1;;;WD)(A;;0x1;;;" + packageGroup + ")";
        Win32(ConvertStringSecurityDescriptorToSecurityDescriptor(sddl, 1, out IntPtr descriptor, out _), "test descriptor");
        IntPtr privileges = Marshal.AllocHGlobal(512);
        IntPtr duplicate = IntPtr.Zero;
        try
        {
            Win32(DuplicateToken(token, 2, out duplicate), "copy suspended token");
            uint size = 512;
            var map = new GenericMapping { Read = 1, Write = 2, Execute = 4, All = 7 };
            Win32(AccessCheck(descriptor, duplicate, 1, ref map, privileges, ref size, out uint grant, out bool status), "package AccessCheck");
            return status && grant == 1;
        }
        finally
        {
            if (duplicate != IntPtr.Zero) CloseHandle(duplicate);
            Marshal.FreeHGlobal(privileges);
            LocalFree(descriptor);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
    [DllImport("kernel32.dll", EntryPoint = "SetInformationJobObject", SetLastError = true)]
    private static extern bool SetJobCpuLimit(SafeFileHandle job, int type, ref CpuLimit limit, uint size);
    [DllImport("kernel32.dll", EntryPoint = "SetInformationJobObject", SetLastError = true)]
    private static extern bool SetJobUiLimits(SafeFileHandle job, int type, ref uint limits, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessMitigationPolicy(nint process, int policy, out uint value, nuint size);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetSecurityDescriptorSacl(nint descriptor, out bool present, out nint sacl, out bool defaulted);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint SetNamedSecurityInfo(string name, uint type, uint information, nint owner, nint group, nint dacl, nint sacl);
    [DllImport("advapi32.dll")]
    private static extern uint GetLengthSid(nint sid);
    [DllImport("advapi32.dll")]
    private static extern nint GetSidSubAuthorityCount(nint sid);
    [DllImport("advapi32.dll")]
    private static extern nint GetSidSubAuthority(nint sid, uint index);

    [StructLayout(LayoutKind.Sequential)]
    private struct CpuLimit
    {
        public uint Flags;
        public uint Rate;
    }

    private const int HandleList = 0x20002;
    private const int SecurityCapabilities = 0x20009;
    private const int ChildProcessPolicy = 0x2000e;
    private const int AllApplicationPackagesPolicy = 0x2000f;
    private const uint SuspendedUnicodeExtendedStartup = 0x00080000 | 0x00000400 | 0x00000004;

    private readonly PreviewSnapshot snapshot;
    private readonly nint sid;
    private readonly SecurityIdentifier package;

    internal LpacPreviewLauncher(PreviewSnapshot snapshot)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException();

        this.snapshot = snapshot;
        int result = CreateAppContainerProfile(snapshot.Owner, snapshot.Owner, "ModernFormsNext isolated preview", 0, 0, out sid);
        if (result != 0)
            Marshal.ThrowExceptionForHR(result);
        try
        {
            var bytes = new byte[GetLengthSid(sid)];
            Marshal.Copy(sid, bytes, 0, bytes.Length);
            package = new SecurityIdentifier(bytes, 0);
            Grant(snapshot.Root, package, FileSystemRights.ReadAndExecute, InheritanceFlags.None);
            Grant(snapshot.WorkerDirectory, package, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit);
            Grant(snapshot.Scratch, package, FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit);
            SetLowIntegrity(snapshot.Scratch);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static void Grant(string directory, SecurityIdentifier package, FileSystemRights rights, InheritanceFlags inheritance)
    {
        var path = new DirectoryInfo(directory);
        var acl = path.GetAccessControl(AccessControlSections.Access);
        acl.AddAccessRule(new FileSystemAccessRule(package, rights, inheritance, PropagationFlags.None, AccessControlType.Allow));
        path.SetAccessControl(acl);
    }

    internal void SealScratch()
    {
        // Only the trusted policy probe needs writable staging. Project code receives a newly
        // empty, inaccessible scratch path. The AppContainer's own OS profile is still separate.
        var directory = new DirectoryInfo(snapshot.Scratch);
        var acl = directory.GetAccessControl(AccessControlSections.Access);
        acl.PurgeAccessRules(package);
        directory.SetAccessControl(acl);
        File.Delete(FilePath.Combine(snapshot.Scratch, "canary.txt"));
    }

    private static void SetLowIntegrity(string path)
    {
        Win32(ConvertStringSecurityDescriptorToSecurityDescriptor("S:(ML;OICI;NW;;;LW)", 1, out nint descriptor, out _), "scratch label");
        try
        {
            Win32(GetSecurityDescriptorSacl(descriptor, out bool present, out nint sacl, out _) && present, "scratch SACL");
            uint code = SetNamedSecurityInfo(path, 1, 0x10, 0, 0, 0, sacl); // SE_FILE_OBJECT, LABEL_SECURITY_INFORMATION
            if (code != 0)
                throw new Win32Exception((int)code, "scratch integrity");
        }
        finally { LocalFree(descriptor); }
    }

    internal WorkerProcess Start(PreviewKey key, string? probeAddress = null, int probePort = 0)
    {
        var input = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        try
        {
            var output = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            try { return Launch(key, input, output, probeAddress, probePort); }
            catch { output.Dispose(); throw; }
        }
        catch { input.Dispose(); throw; }
    }

    private WorkerProcess Launch(PreviewKey key, AnonymousPipeServerStream input, AnonymousPipeServerStream output,
        string? probeAddress, int probePort)
    {
        nint size = 0;
        InitializeProcThreadAttributeList(0, 4, 0, ref size);
        if (size == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "attribute list sizing");
        nint attributes = Marshal.AllocHGlobal(size);
        nint caps = Marshal.AllocHGlobal(Marshal.SizeOf<Capabilities>());
        nint optOut = Marshal.AllocHGlobal(sizeof(int));
        nint childPolicy = Marshal.AllocHGlobal(sizeof(int));
        nint handles = Marshal.AllocHGlobal(2 * IntPtr.Size);
        string system = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string extras = probeAddress is null ? "" :
            FormattableString.Invariant($"MFN_PREVIEW_ADDRESS={probeAddress}\0MFN_PREVIEW_CANARY={FilePath.Combine(snapshot.Root, "outside.txt")}\0MFN_PREVIEW_PARENT={Environment.ProcessId}\0MFN_PREVIEW_PORT={probePort}\0");
        // No inherited PATH, CLR profiler settings, user profile paths or project environment.
        nint environment = Marshal.StringToHGlobalUni(
            $"LOCALAPPDATA={snapshot.Scratch}\0{extras}SystemRoot={system}\0TEMP={snapshot.Scratch}\0TMP={snapshot.Scratch}\0\0");
        using var job = CreateJobObject(0, null);
        ProcessInfo process = default;
        bool initialized = false, transferred = false;
        try
        {
            Win32(!job.IsInvalid, "create job");
            var limit = new JobLimits { Flags = 0x2000 | 8 | 0x100, Processes = 1, ProcessMemory = (nuint)(384U << 20) };
            Win32(SetInformationJobObject(job, 9, ref limit, (uint)Marshal.SizeOf<JobLimits>()), "job limits");
            // JobObjectBasicUIRestrictions denies external USER handles, clipboard, desktop
            // creation/switching and global desktop settings. Never share a Designer HWND.
            uint ui = 0xff;
            Win32(SetJobUiLimits(job, 4, ref ui, sizeof(uint)), "job UI restrictions");
            var cpu = new CpuLimit { Flags = 0x5, Rate = 2500 };
            Win32(SetJobCpuLimit(job, 15, ref cpu, (uint)Marshal.SizeOf<CpuLimit>()), "job CPU limit");
            Win32(InitializeProcThreadAttributeList(attributes, 4, 0, ref size), "attribute list");
            initialized = true;
            Marshal.StructureToPtr(new Capabilities { Sid = sid }, caps, false);
            SetAttribute(SecurityCapabilities, caps, Marshal.SizeOf<Capabilities>());
            Marshal.WriteInt32(childPolicy, 1);
            SetAttribute(ChildProcessPolicy, childPolicy, sizeof(int));
            Marshal.WriteInt32(optOut, 1);
            SetAttribute(AllApplicationPackagesPolicy, optOut, sizeof(int));
            Marshal.WriteIntPtr(handles, input.ClientSafePipeHandle.DangerousGetHandle());
            Marshal.WriteIntPtr(handles, IntPtr.Size, output.ClientSafePipeHandle.DangerousGetHandle());
            SetAttribute(HandleList, handles, 2 * IntPtr.Size);

            string executable = FilePath.Combine(snapshot.WorkerDirectory, "ModernFormsNext.DesignerPreviewWorker.exe");
            var startup = new Startup { Size = Marshal.SizeOf<Startup>(), Attributes = attributes };
            var command = new StringBuilder(FormattableString.Invariant($"\"{executable}\" {input.GetClientHandleAsString()} {output.GetClientHandleAsString()} {key.Session:N} {key.Request} {key.Revision} {key.Node}"));
            Win32(CreateProcess(executable, command, 0, 0, true, SuspendedUnicodeExtendedStartup,
                environment, snapshot.WorkerDirectory, ref startup, out process), "create suspended LPAC worker");
            input.DisposeLocalCopyOfClientHandle();
            output.DisposeLocalCopyOfClientHandle();
            Win32(AssignProcessToJobObject(job, process.Process), "assign suspended worker");
            Win32(IsProcessInJob(process.Process, job, out bool present) && present, "worker job membership");
            VerifyToken(process.Process);
            Win32(GetProcessMitigationPolicy(process.Process, 13, out uint child, sizeof(uint)) && (child & 1) != 0, "child process mitigation");
            Win32(ResumeThread(process.Thread) == 1, "resume LPAC worker");
            var result = new WorkerProcess(new SafeFileHandle(job.DangerousGetHandle(), ownsHandle: true), process.Process, input, output);
            job.SetHandleAsInvalid();
            transferred = true;
            return result;
        }
        finally
        {
            if (!transferred && process.Process != 0)
            {
                TerminateProcess(process.Process, 1);
                WaitForSingleObject(process.Process, 5000);
                CloseHandle(process.Process);
            }
            if (process.Thread != 0)
                CloseHandle(process.Thread);
            if (initialized)
                DeleteProcThreadAttributeList(attributes);
            Marshal.FreeHGlobal(attributes);
            Marshal.FreeHGlobal(caps);
            Marshal.FreeHGlobal(optOut);
            Marshal.FreeHGlobal(childPolicy);
            Marshal.FreeHGlobal(handles);
            Marshal.FreeHGlobal(environment);
        }

        void SetAttribute(int kind, nint data, int bytes)
            => Win32(UpdateProcThreadAttribute(attributes, 0, kind, data, bytes, 0, 0), "process attributes");
    }

    private void VerifyToken(nint process)
    {
        Win32(OpenProcessToken(process, 10, out nint token), "worker token");
        nint data = Marshal.AllocHGlobal(512);
        try
        {
            if (TokenNumber(token, 29, data) != 1 ||
                HasAccessThroughPackageGroup(token, "S-1-15-2-1") ||
                !HasAccessThroughPackageGroup(token, "S-1-15-2-2"))
                throw new UnauthorizedAccessException("Expected LPAC token.");
            Win32(GetTokenInformation(token, 31, data, 512, out _), "worker package SID");
            if (!EqualSid(Marshal.ReadIntPtr(data), sid))
                throw new UnauthorizedAccessException("Unexpected worker identity.");
            Win32(GetTokenInformation(token, 25, data, 512, out _), "worker integrity");
            nint integrity = Marshal.ReadIntPtr(data);
            int count = Marshal.ReadByte(GetSidSubAuthorityCount(integrity));
            if (count < 1 || Marshal.ReadInt32(GetSidSubAuthority(integrity, (uint)count - 1)) != 0x1000)
                throw new UnauthorizedAccessException("Expected Low integrity.");
            Win32(GetTokenInformation(token, 30, data, 512, out _), "worker capabilities");
            if (Marshal.ReadInt32(data) != 0)
                throw new UnauthorizedAccessException("Unexpected worker capabilities.");
        }
        finally
        {
            Marshal.FreeHGlobal(data);
            CloseHandle(token);
        }
    }

    private static void Win32(bool succeeded, string operation)
    {
        if (!succeeded)
            throw new Win32Exception(Marshal.GetLastWin32Error(), operation);
    }

    public void Dispose()
    {
        FreeSid(sid);
        int result = DeleteAppContainerProfile(snapshot.Owner);
        if (result != 0)
            Marshal.ThrowExceptionForHR(result);
    }

    internal sealed class WorkerProcess(SafeFileHandle job, nint process, Stream input, Stream output) : IDisposable
    {
        internal Stream Input => input;
        internal Stream Output => output;

        // Called by the deadline even if an anonymous-pipe operation is blocked in the kernel.
        internal void Kill() => TerminateJobObject(job, 1);

        internal uint? ExitStatus => WaitForSingleObject(process, 0) == 0 && GetExitCodeProcess(process, out uint code) ? code : null;

        internal bool ExitedSuccessfully(uint milliseconds)
            => WaitForSingleObject(process, milliseconds) == 0 && GetExitCodeProcess(process, out uint code) && code == 0;

        public void Dispose()
        {
            job.Dispose();
            WaitForSingleObject(process, 5000);
            CloseHandle(process);
            input.Dispose();
            output.Dispose();
        }
    }
}
