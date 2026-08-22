using System.ComponentModel;
using System.Runtime.InteropServices;

namespace EventViewerX;

/// <summary>Reads effective local advanced audit policy through the culture-independent Windows API.</summary>
public static class AuditPolicyReader {
    private const uint AuditSuccess = 1;
    private const uint AuditFailure = 2;

    /// <summary>Reads effective local policy for distinct audit subcategory GUIDs.</summary>
    public static IReadOnlyList<EffectiveAuditPolicyResult> Query(IEnumerable<Guid> subcategoryGuids) {
        if (subcategoryGuids == null) {
            throw new ArgumentNullException(nameof(subcategoryGuids));
        }
        Guid[] requested = subcategoryGuids.Distinct().ToArray();
        if (requested.Length == 0) {
            return Array.Empty<EffectiveAuditPolicyResult>();
        }
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return requested.Select(static guid => new EffectiveAuditPolicyResult(
                guid,
                false,
                EventAuditOutcome.None,
                0,
                "Effective audit policy is available only on Windows.")).ToArray();
        }

        IntPtr buffer = IntPtr.Zero;
        try {
            if (!AuditQuerySystemPolicy(requested, (uint)requested.Length, out buffer)) {
                int error = Marshal.GetLastWin32Error();
                string message = new Win32Exception(error).Message;
                return requested.Select(guid => new EffectiveAuditPolicyResult(
                    guid,
                    false,
                    EventAuditOutcome.None,
                    error,
                    message)).ToArray();
            }
            int size = Marshal.SizeOf<AuditPolicyInformation>();
            var results = new Dictionary<Guid, EffectiveAuditPolicyResult>();
            for (int index = 0; index < requested.Length; index++) {
                IntPtr itemPointer = IntPtr.Add(buffer, checked(index * size));
                AuditPolicyInformation item = Marshal.PtrToStructure<AuditPolicyInformation>(itemPointer);
                EventAuditOutcome outcomes = EventAuditOutcome.None;
                if ((item.AuditingInformation & AuditSuccess) != 0) {
                    outcomes |= EventAuditOutcome.Success;
                }
                if ((item.AuditingInformation & AuditFailure) != 0) {
                    outcomes |= EventAuditOutcome.Failure;
                }
                results[item.AuditSubCategoryGuid] = new EffectiveAuditPolicyResult(
                    item.AuditSubCategoryGuid,
                    true,
                    outcomes,
                    0,
                    null);
            }
            return requested.Select(guid => results.TryGetValue(guid, out EffectiveAuditPolicyResult? result)
                ? result
                : new EffectiveAuditPolicyResult(guid, false, EventAuditOutcome.None, 0,
                    "Windows did not return the requested audit subcategory.")).ToArray();
        } catch (Exception exception) {
            int error = Marshal.GetLastWin32Error();
            return requested.Select(guid => new EffectiveAuditPolicyResult(
                guid,
                false,
                EventAuditOutcome.None,
                error,
                exception.Message)).ToArray();
        } finally {
            if (buffer != IntPtr.Zero) {
                AuditFree(buffer);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AuditPolicyInformation {
        internal Guid AuditSubCategoryGuid;
        internal uint AuditingInformation;
        internal Guid AuditCategoryGuid;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AuditQuerySystemPolicy(
        [In] Guid[] subCategoryGuids,
        uint policyCount,
        out IntPtr auditPolicy);

    [DllImport("advapi32.dll")]
    private static extern void AuditFree(IntPtr buffer);
}
