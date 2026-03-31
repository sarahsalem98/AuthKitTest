using AuthKit.Attributes;
using AuthKit.Contracts;
using AuthKit.Services;
using AuthKitTest.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace AuthKitTest.Api.Controllers;

[ApiController]
[Route("api/vendor")]
[RequirePortal("vendor")]
public class VendorDashboardController : ControllerBase
{
    private readonly ICurrentUser _currentUser;

    public VendorDashboardController(ICurrentUser currentUser)
        => _currentUser = currentUser;

    /// <summary>Any vendor portal user can access their profile.</summary>
    [HttpGet("profile")]
    public IActionResult Profile() => Ok(new
    {
        userId      = _currentUser.UserId,
        portal      = _currentUser.PortalKey,
        roles       = _currentUser.Roles,
        permissions = _currentUser.Permissions
    });

    /// <summary>Requires read:catalog — all vendors can browse the catalog.</summary>
    [HttpGet("catalog")]
    [RequirePermission("read:catalog")]
    public IActionResult GetCatalog() => Ok(new[]
    {
        new { sku = "SKU-001", description = "Raw Material X", unitPrice = 5.00 },
        new { sku = "SKU-002", description = "Component Y",    unitPrice = 12.50 }
    });

    /// <summary>Requires submit:quotes — vendors can submit price quotes.</summary>
    [HttpPost("quotes")]
    [RequirePermission("submit:quotes")]
    public IActionResult SubmitQuote([FromBody] object payload) => Ok("quote submitted");

    /// <summary>Requires manage:contracts — only VendorManager role.</summary>
    [HttpGet("contracts")]
    [RequireRole("VendorManager")]
    [RequirePermission("manage:contracts")]
    public IActionResult GetContracts() => Ok(new[]
    {
        new { contractId = "CTR-001", status = "active",  value = 15000.00 },
        new { contractId = "CTR-002", status = "expired", value = 8500.00  }
    });

    /// <summary>Requires read:invoices — vendors view their own invoices.</summary>
    [HttpGet("invoices")]
    [RequirePermission("read:invoices")]
    public IActionResult GetInvoices() => Ok(new[]
    {
        new { invoiceId = "VINV-2024-001", amount = 3200.00, status = "paid" }
    });

    /// <summary>Manager-only: revoke a vendor user's tokens.</summary>
    [HttpDelete("users/{id}")]
    [RequireRole("VendorManager")]
    public async Task<IActionResult> RevokeUser(
        string id,
        [FromServices] AuthService<AppUser, VendorLoginModel, VendorRegisterModel> auth,
        CancellationToken ct)
    {
        await auth.RevokeAsync(id, ct);
        return NoContent();
    }
}
