using Devices.API.DTO.PersonDTOs;

namespace Devices.API.DTO.EmployeeDTOs;

public record CreateEmployeeDTO(
    PersonDTO Person,
    decimal Salary,
    int PositionId
);
