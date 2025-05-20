namespace Devices.API.DTO.EmployeeDTOs;

public record EmployeeByIdDTO(
    string PassportNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    string PhoneNumber,
    string Email,
    decimal Salary,
    PositionDTO Position,
    DateTime HireDate    
    );