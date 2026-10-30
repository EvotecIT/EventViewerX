using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EventViewerX.Cli;

internal static partial class Program {
    // Existing files compare by volume/device and file ID, so hard links cannot bypass output guards.
    // Missing paths still compare by their resolved parent and normalized destination name.
    private static string ComparisonPath(string path) {
        if (OperatingSystem.IsWindows()) {
            if (path.StartsWith("\\\\?\\UNC\\", StringComparison.OrdinalIgnoreCase)) { path = "\\\\" + path.Substring(8); }
            else if (path.StartsWith("\\\\?\\", StringComparison.Ordinal)) { path = path.Substring(4); }
        }
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)!;
        string resolved = root;
        foreach (string segment in fullPath.Substring(root.Length)
                     .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)) {
            resolved = Path.Combine(resolved, segment);
            FileAttributes attributes;
            try {
                attributes = File.GetAttributes(resolved);
            } catch (FileNotFoundException) {
                continue;
            } catch (DirectoryNotFoundException) {
                continue;
            }
            if ((attributes & FileAttributes.ReparsePoint) == 0) { continue; }
            FileSystemInfo link = (attributes & FileAttributes.Directory) != 0
                ? new DirectoryInfo(resolved) : new FileInfo(resolved);
            resolved = link.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                ?? throw new IOException($"Cannot resolve output path link '{resolved}'.");
        }
        if (!File.Exists(resolved)) { return "path:" + Path.GetFullPath(resolved); }
        if (OperatingSystem.IsWindows()) {
            using SafeFileHandle handle = File.OpenHandle(resolved, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            // FileIdInfo includes the full 128-bit ID used by ReFS as well as NTFS.
            if (!GetFileInformationByHandleEx(handle, 18, out WindowsFileId info, 24)) {
                throw FileIdentityError(resolved);
            }
            return $"file:{info.Volume:X16}:{info.Low:X16}:{info.High:X16}";
        }
        if (OperatingSystem.IsLinux()) {
            // statx has one fixed layout on both supported Linux architectures.
            if (Statx(-100, resolved, 0, 0x100, out LinuxFileId info) != 0 || (info.Mask & 0x100) == 0) {
                throw FileIdentityError(resolved);
            }
            return $"file:{info.DeviceMajor:X8}:{info.DeviceMinor:X8}:{info.Inode:X16}";
        }
        if (OperatingSystem.IsMacOS()) {
            int result = RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? StatDarwin64(resolved, out DarwinFileId info)
                : StatDarwin(resolved, out info);
            if (result != 0) { throw FileIdentityError(resolved); }
            return $"file:{info.Device:X8}:{info.Inode:X16}";
        }
        throw new PlatformNotSupportedException("File identity validation supports Windows, Linux and macOS.");
    }

    private static IOException FileIdentityError(string path) =>
        new($"Cannot verify file identity for '{path}'.", new Win32Exception(Marshal.GetLastPInvokeError()));

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsFileId {
        public ulong Volume;
        public ulong Low;
        public ulong High;
    }

    // Native statx and Darwin stat64 ABI sizes; only identity fields are consumed.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxFileId {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(32)] public ulong Inode;
        [FieldOffset(136)] public uint DeviceMajor;
        [FieldOffset(140)] public uint DeviceMinor;
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct DarwinFileId {
        [FieldOffset(0)] public uint Device;
        [FieldOffset(8)] public ulong Inode;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass,
        out WindowsFileId information, uint bufferSize);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags, uint mask, out LinuxFileId information);

    [DllImport("libSystem.B.dylib", EntryPoint = "stat$INODE64", SetLastError = true)]
    private static extern int StatDarwin64([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out DarwinFileId information);

    [DllImport("libSystem.B.dylib", EntryPoint = "stat", SetLastError = true)]
    private static extern int StatDarwin([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out DarwinFileId information);
}
