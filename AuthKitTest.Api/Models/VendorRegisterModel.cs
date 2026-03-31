namespace AuthKitTest.Api.Models;

public record VendorRegisterModel(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string VendorName
);
