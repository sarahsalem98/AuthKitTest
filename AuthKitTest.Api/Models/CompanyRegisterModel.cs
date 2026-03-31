namespace AuthKitTest.Api.Models;

public record CompanyRegisterModel(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string CompanyName
);
