namespace Devices.API.DTO;

public record EmployeeDTO(
    string PassportNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    string PhoneNumber,
    string Email);