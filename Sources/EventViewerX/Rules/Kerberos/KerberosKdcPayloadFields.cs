using System.Globalization;

namespace EventViewerX.Rules.Kerberos;

/// <summary>
/// Reads the version-zero KDCsvc 201-209 event template by provider payload
/// position so projections do not depend on localized message labels.
/// </summary>
internal sealed class KerberosKdcPayloadFields
{
    internal string CipherName { get; private set; } = string.Empty;
    internal string EnabledInsecureCiphers { get; private set; } = string.Empty;
    internal string AccountName { get; private set; } = string.Empty;
    internal string SuppliedRealmName { get; private set; } = string.Empty;
    internal string AccountSupportedEncryptionTypes { get; private set; } = string.Empty;
    internal string AccountAvailableKeys { get; private set; } = string.Empty;
    internal string ServiceName { get; private set; } = string.Empty;
    internal string ServiceSid { get; private set; } = string.Empty;
    internal string ServiceSupportedEncryptionTypes { get; private set; } = string.Empty;
    internal string ServiceAvailableKeys { get; private set; } = string.Empty;
    internal string DomainControllerSupportedEncryptionTypes { get; private set; } = string.Empty;
    internal string DefaultDomainSupportedEncTypes { get; private set; } = string.Empty;
    internal string DomainControllerAvailableKeys { get; private set; } = string.Empty;
    internal string ClientAddress { get; private set; } = string.Empty;
    internal string ClientPort { get; private set; } = string.Empty;
    internal string ClientAdvertizedEncryptionTypes { get; private set; } = string.Empty;

    internal static KerberosKdcPayloadFields Parse(EventObject source)
    {
        var result = new KerberosKdcPayloadFields();
        if (source.Version.HasValue && source.Version.Value != 0) {
            return result;
        }

        IReadOnlyList<EventPropertyValue> values = source.Properties;
        if (source.Id == 205) {
            if (values.Count >= 2) {
                result.EnabledInsecureCiphers = Read(values, 0);
                result.DefaultDomainSupportedEncTypes = Read(values, 1);
            }
            return result;
        }

        if (source.Id is < 201 or > 209 || values.Count < 15) {
            return result;
        }

        result.CipherName = Read(values, 0);
        result.AccountName = Read(values, 1);
        result.SuppliedRealmName = Read(values, 2);
        result.AccountSupportedEncryptionTypes = Read(values, 3);
        result.AccountAvailableKeys = Read(values, 4);
        result.ServiceName = Read(values, 5);
        result.ServiceSid = Read(values, 6);
        result.ServiceSupportedEncryptionTypes = Read(values, 7);
        result.ServiceAvailableKeys = Read(values, 8);
        result.DomainControllerSupportedEncryptionTypes = Read(values, 9);
        result.DefaultDomainSupportedEncTypes = Read(values, 10);
        result.DomainControllerAvailableKeys = Read(values, 11);
        result.ClientAddress = Read(values, 12);
        result.ClientPort = Read(values, 13);
        result.ClientAdvertizedEncryptionTypes = Read(values, 14);
        return result;
    }

    private static string Read(IReadOnlyList<EventPropertyValue> values, int index)
    {
        object? value = values[index].Value;
        return value == null
            ? string.Empty
            : Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }
}
