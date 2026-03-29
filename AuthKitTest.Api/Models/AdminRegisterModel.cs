namespace AuthKitTest.Api.Models;

public record AdminRegisterModel(
    string Email,
    string Password,
    string FirstName,
    string LastName
);
