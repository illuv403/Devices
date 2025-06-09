namespace Devices.API.DTO.DeviceDTOs;

public record DeviceDTO(
    string Name,
    bool IsEnabled,
    Dictionary<string, string> AdditionalProperties,
    int TypeId
    );