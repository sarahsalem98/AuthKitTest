using AuthKit.Contracts;

namespace AuthKitTest.Api.Auth.Portals;

public class AdminPortalPolicy : IPortalPolicy
{
    public string PortalKey => "admin";
    public IReadOnlyList<string> AllowedRoles => new[] { "Admin", "SuperAdmin" };
    public IReadOnlyList<string> AllowedPermissions => Array.Empty<string>();
}
