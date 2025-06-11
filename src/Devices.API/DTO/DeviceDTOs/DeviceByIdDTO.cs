using Devices.API.DTO.EmployeeDTOs;

namespace Devices.API.DTO.DeviceDTOs;

public record DeviceByIdDTO(
    string Name, 
    bool IsEnabled,
    Dictionary<string, string> AdditionalProperties,
    string Type
    );