using Devices.API.DTO.EmployeeDTOs;

namespace Devices.API.DTO.DeviceDTOs;

public record DeviceByIdDTO(
    string Name, 
    string DeviceTypeName, 
    bool IsEnabled,
    object AdditionalProperties,
    ShortEmployeeDTO? Employee
    );