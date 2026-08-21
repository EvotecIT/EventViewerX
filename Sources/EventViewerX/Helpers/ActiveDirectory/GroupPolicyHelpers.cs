using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;

namespace EventViewerX.Helpers.ActiveDirectory;

/// <summary>
/// Query Active Directory for Group Policy objects.
/// </summary>
public class GroupPolicyHelpers {
    /// <summary>
    /// Query Active Directory Forest for a Group Policy object by its DistinguishedName.
    /// </summary>
    /// <param name="gpoDn"></param>
    /// <returns></returns>
    public static GroupPolicy? QueryGroupPolicyByDistinguishedName(string gpoDn) {
        try {
            Forest currentForest = Forest.GetCurrentForest();
            foreach (Domain domain in currentForest.Domains) {
                var policy = QueryGroupPolicyInDomainByDistinguishedName(domain.Name, gpoDn);
                if (policy != null) {
                    return policy;
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"Error querying group policy by DN: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Query Active Directory Domain for a Group Policy object by its DistinguishedName.
    /// </summary>
    /// <param name="domainName"></param>
    /// <param name="gpoDn"></param>
    /// <returns></returns>
    private static GroupPolicy? QueryGroupPolicyInDomainByDistinguishedName(string domainName, string gpoDn) {
        try {
            using DirectoryEntry rootDSE = new DirectoryEntry($"LDAP://{domainName}/CN=Policies,CN=System,DC={domainName.Replace(".", ",DC=")}");
            using (DirectorySearcher searcher = new DirectorySearcher(rootDSE)) {
                searcher.Filter = $"(&(objectClass=groupPolicyContainer)(distinguishedName={gpoDn}))";
                searcher.PropertiesToLoad.Add("displayName");
                searcher.PropertiesToLoad.Add("name");
                searcher.PropertiesToLoad.Add("distinguishedName");

                SearchResult? result = searcher.FindOne();
                if (result != null) {
                    string gpoName = result.Properties["displayName"].Count > 0
                        ? result.Properties["displayName"][0]?.ToString() ?? string.Empty
                        : string.Empty;
                    string gpoId = result.Properties["name"].Count > 0
                        ? result.Properties["name"][0]?.ToString() ?? string.Empty
                        : string.Empty;
                    return new GroupPolicy {
                        GpoName = gpoName,
                        GpoId = gpoId,
                        GpoDomain = domainName
                    };
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"Error querying group policy by DN in domain {domainName}: {ex.Message}");
        }
        return null;
    }
}
