using Devices.API.DTO.PersonDTOs;

namespace Devices.API.DTO.EmployeeDTOs;

public record EmployeeByIdDTO(
    PersonDTO person, 
    decimal salary,
    string position,
    string hireDate
    );