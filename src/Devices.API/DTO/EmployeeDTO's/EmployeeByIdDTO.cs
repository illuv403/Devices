namespace Devices.API.DTO;

public record EmployeeByIdDTO(
    EmployeeDTO Employee,
    decimal Salary,
    PositionDTO Position,
    DateTime HireDate    
    );