using AuthKit.Attributes;
using AuthKit.Contracts;
using AuthKit.Services;
using AuthKitTest.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace AuthKitTest.Api.Controllers;

[ApiController]
[Route("api/company")]
[RequirePortal("company")]
public class CompanyDashboardController : ControllerBase
{
    private readonly ICurrentUser _currentUser;

    public CompanyDashboardController(ICurrentUser currentUser)
        => _currentUser = currentUser;

    /// <summary>Any company portal user can access their profile.</summary>
    [HttpGet("profile")]
    public IActionResult Profile() => Ok(new
    {
        userId      = _currentUser.UserId,
        portal      = _currentUser.PortalKey,
        roles       = _currentUser.Roles,
        permissions = _currentUser.Permissions
    });

    /// <summary>Requires read:products permission — test per-permission gating.</summary>
    [HttpGet("products")]
    [RequirePermission("read:products")]
    public IActionResult GetProducts() => Ok(new[]
    {
        new { id = 1, name = "Widget A", price = 9.99 },
        new { id = 2, name = "Widget B", price = 19.99 }
    });

    /// <summary>Requires write:products — managers can add products.</summary>
    [HttpPost("products")]
    [RequirePermission("write:products")]
    public IActionResult CreateProduct([FromBody] object payload) => Ok("product created");

    /// <summary>Requires manage:orders — only CompanyManager role.</summary>
    [HttpGet("orders")]
    [RequireRole("CompanyManager")]
    [RequirePermission("manage:orders")]
    public IActionResult GetOrders() => Ok(new[]
    {
        new { orderId = "ORD-001", status = "pending", total = 250.00 },
        new { orderId = "ORD-002", status = "shipped", total = 99.50 }
    });

    /// <summary>Requires read:invoices — billing view accessible to CompanyUser too.</summary>
    [HttpGet("invoices")]
    [RequirePermission("read:invoices")]
    public IActionResult GetInvoices() => Ok(new[]
    {
        new { invoiceId = "INV-2024-001", amount = 500.00, due = "2024-02-01" }
    });

    /// <summary>Manager-only: suspend a company user's tokens.</summary>
    [HttpDelete("users/{id}")]
    [RequireRole("CompanyManager")]
    public async Task<IActionResult> SuspendUser(
        string id,
        [FromServices] AuthService<AppUser, CompanyLoginModel, CompanyRegisterModel> auth,
        CancellationToken ct)
    {
        await auth.RevokeAsync(id, ct);
        return NoContent();
    }
}
