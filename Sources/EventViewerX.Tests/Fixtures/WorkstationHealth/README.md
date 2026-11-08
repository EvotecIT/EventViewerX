# Workstation health fixtures

These XML files preserve captured provider payload layouts. Computer names and
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
