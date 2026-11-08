# Workstation health fixtures

These XML files preserve captured provider payload layouts and explicitly identified native schema examples. Computer names and
security/correlation identifiers are removed or replaced; binary attachments are
omitted because these projections use insertion strings. The local application
and service identities and NTFS volume/device names are synthetic replacements.
XML escaping and namespace declarations are normalized where the published sample
was formatted as text rather than valid XML.

- `application-crash-1000`, `service-failure-7031` and `ntfs-healthy-98` are
  redacted local Windows 11 captures.
- `windows-update-failure-20` is a local Windows 11 System event from
  Microsoft-Windows-WindowsUpdateClient. The Store app title, GUIDs, timestamp,
  computer, record and execution identifiers are synthetic replacements; the
  lowercase data names and `0x80073d02` error code are preserved.
- `bitlocker-suspended-773` follows the suspension template from the installed
  Windows 11 Microsoft-Windows-BitLocker-API provider manifest. Its three data
  fields are `IdentificationGUID`, `VolumeName` and `VolumeMountPoint`; values
  and system metadata are synthetic. It is a schema fixture, not a local capture.
- `application-hang-1002`, `service-failure-7034`, `ntfs-corruption-55`,
  `boot-performance-100`, `startup-degradation-101` and
  `shutdown-performance-200` come from the example event data in
  [Eric Zimmerman's evtx maps](https://github.com/EricZimmerman/evtx/tree/master/evtx/Maps).
  Their MIT license is retained in `evtx-maps-LICENSE.txt`.
- `hardware-corrected-17` and `display-reset-4101` come from the reporter's XML in
  [ValveSoftware/Dota2-Gameplay #13507](https://github.com/ValveSoftware/Dota2-Gameplay/issues/13507).
- `disk-retry-153` comes from the reporter's XML in
  [Microsoft Q&A question 435089](https://learn.microsoft.com/en-ca/answers/questions/435089/getting-tons-of-warning-153-on-a-server2012-machin).
- `storage-reset-129` comes from the reporter's XML in
  [Microsoft Q&A question 4208491](https://learn.microsoft.com/en-us/answers/questions/4208491/thunderbolt-ssd-stornvme-issue-windows-10).
- `shutdown-degradation-203` comes from the reporter's XML in
  [Microsoft Q&A question 3759159](https://learn.microsoft.com/es-es/answers/questions/3759159/error-en-visor-de-eventos).

The tests also create explicitly derived variants. Named Application Error data
is converted to classic insertion strings; SCM 7000/7009/7023 payloads use the
documented parameter layouts; disk 7/51 reuse the captured device insertion;
NTFS 98 uses a positive action state. WHEA 18/19/47 and startup 102–110 variants
use the field names and component meanings from the provider metadata installed
on Windows 11. These variants are schema checks, not additional captures from the
workstation. The same captured files are embedded in the Native AOT smoke.

Additional rule audit fixtures retain the source layout while replacing machine,
account, directory, network and task identities:

- `dfs-replication-error-5002` is a redacted Windows Server capture. Its unnamed
  insertion strings identify the partner, replication group and error code.
- `defender-configuration-5007` is a redacted Windows 11 capture. The old and new
  setting paths are synthetic; the native space-separated data names remain.
- `scheduled-task-created-4698` is a redacted Windows Server capture containing
  namespaced Task Scheduler registration metadata. Unneeded task sections are
  omitted, and the action is replaced with an inert command. The registration
  date intentionally has no time zone.
- `privileged-service-4673` is a redacted Windows Server capture used to reject
  generic privilege-use events as BitLocker key changes.
- `bitlocker-key-protector-775` follows the installed BitLocker provider schema.
  Values are synthetic; it is not a captured protector change.
- `certificate-request-4886` and `certificate-issued-4887-v0/v1` follow the
  installed Windows Server Security-Auditing templates. Their values are
  synthetic. A request received is separate from a certificate issued; serial
  numbers are preserved as hexadecimal text rather than decoded as characters.
- `security-log-backup-1105` follows the Eventlog provider's backup event schema
  with a synthetic path. Automatic backup is not log clearing.
- `network-monitor-service-7045` is derived from a captured Windows 11 Service
  Control Manager insertion layout. The service identity and driver role are replaced with Npcap
  to exercise structured driver matching; it is not evidence of a real Npcap
  installation on the captured machine.
- `iis-binding-failure-1007` follows the IIS W3SVC provider template inspected on
  Windows Server. Its URL and numeric site identifier are synthetic; it is not
  a captured binding failure.

Hyper-V lifecycle source assertions use Microsoft's
[Hyper-V administrative guidance](https://download.microsoft.com/download/7/5/0/750db292-f3d3-48c9-9557-aa64237a0e22/Virtualization%201909%20Administrative%20Guide.pdf)
and the installed provider's channel links. They do not claim live Hyper-V
lifecycle coverage on a machine without the Hyper-V host role.

`filter-manager-driver-6` is a redacted Windows 11 FilterManager capture of an
ordinary filesystem filter. It verifies that filesystem-filter loads are not
classified as packet-monitor driver activity. The supported packet-monitor
source uses Service Control Manager installation/control events, which do not
by themselves prove a driver load.
