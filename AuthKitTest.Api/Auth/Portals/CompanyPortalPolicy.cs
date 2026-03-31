using AuthKit.Contracts;

namespace AuthKitTest.Api.Auth.Portals;

public class CompanyPortalPolicy : IPortalPolicy
{
    public string PortalKey => "company";
    public IReadOnlyList<string> AllowedRoles => new[] { "CompanyManager", "CompanyUser" };
    public IReadOnlyList<string> AllowedPermissions => Array.Empty<string>();
}
