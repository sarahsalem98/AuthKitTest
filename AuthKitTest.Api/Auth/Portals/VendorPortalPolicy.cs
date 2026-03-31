using AuthKit.Contracts;

namespace AuthKitTest.Api.Auth.Portals;

public class VendorPortalPolicy : IPortalPolicy
{
    public string PortalKey => "vendor";
    public IReadOnlyList<string> AllowedRoles => new[] { "VendorManager", "VendorSupplier" };
    public IReadOnlyList<string> AllowedPermissions => new[] { "read:catalog", "read:invoices" };
}
