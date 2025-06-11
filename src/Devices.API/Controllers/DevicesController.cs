using System.Text.Json;
using Devices.API.DTO.DeviceDTOs;
using Devices.API.DTO.EmployeeDTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Devices.API.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    private readonly DevicesDbContext _context;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(DevicesDbContext context, ILogger<DevicesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllDevices(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing GET request on api/devices");

            var devices = await _context.Devices
                .Select(d => new ShortDevicesDTO(
                    d.Id,
                    d.Name
                ))
                .ToListAsync(cancellationToken);

            _logger.LogInformation($"Retrieved {devices.Count} devices, execution succeeded");
            return Ok(devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GET /api/devices: {ex.Message}");
            return Problem(title: "Something went wrong", detail: ex.Message);
        }
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetDeviceById(int id, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation($"Executing GET request on api/devices/{id}");

            var device = await _context.Devices
                .Include(d => d.DeviceType)
                .Include(d => d.DeviceEmployees.Where(de => de.ReturnDate == null))
                .ThenInclude(de => de.Employee)
                .ThenInclude(e => e.Person)
                .Where(d => d.Id == id)
                .Select(d => new
                {
                    d.Name,
                    d.IsEnabled,
                    d.AdditionalProperties,
                    Type = d.DeviceType.Name,
                    Employee = d.DeviceEmployees
                        .Where(de => de.ReturnDate == null)
                        .Select(de => new
                        {
                            de.Employee.Id,
                            FullName = de.Employee.Person.FirstName + " " + de.Employee.Person.LastName
                        })
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (device == null)
            {
                _logger.LogInformation($"No device found with id {id}");
                return NotFound();
            }

            var username = User.FindFirstValue("sub");
            var role = User.FindFirstValue("role");

            if (role != "Admin")
            {
                var userAccount = await _context.Accounts
                    .Include(a => a.Employee)
                    .FirstOrDefaultAsync(a => a.Username == username, cancellationToken);

                if (userAccount == null)
                {
                    _logger.LogInformation($"No account found with username {username}");
                    return Forbid();
                }

                var isAssigned = await _context.DeviceEmployees
                    .AnyAsync(de => de.DeviceId == id && de.EmployeeId == userAccount.Employee.Id && de.ReturnDate == null, cancellationToken);

                if (!isAssigned)
                {
                    _logger.LogInformation("No devices assigned to current user");
                    return Forbid();
                }
            }

            var deviceDTO = new DeviceByIdDTO(
                device.Name,
                device.IsEnabled,
                JsonSerializer.Deserialize<Dictionary<string, string>>(device.AdditionalProperties),
                device.Type
            );

            _logger.LogInformation("Execution succeeded");
            return Ok(deviceDTO);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GET /api/devices/{id}: {ex.Message}");
            return Problem(title: "Something went wrong", detail: ex.Message);
        }
    }

    [HttpGet("types")]
    public async Task<IActionResult> GetDevicesTypes(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing GET request on api/devices/types");

            var deviceTypes = await _context.DeviceTypes
                .Select(dt => new DeviceTypesDTO(dt.Id, dt.Name))
                .ToListAsync(cancellationToken);

            _logger.LogInformation($"Retrieved {deviceTypes.Count} device types, execution succeeded");
            return Ok(deviceTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GET /api/devices/types: {ex.Message}");
            return Problem(title: "Something went wrong", detail: ex.Message);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddDevice([FromBody] DeviceDTO device, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing POST request on api/devices");

            var deviceType = await _context.DeviceTypes
                .FirstOrDefaultAsync(dt => dt.Name == device.Type, cancellationToken);

            if (deviceType == null)
            {
                _logger.LogInformation($"No device type found with name '{device.Type}'");
                return NotFound("Device type not found");
            }

            var serializedProps = JsonSerializer.Serialize(device.AdditionalProperties);

            var newDevice = new Device
            {
                Name = device.Name,
                IsEnabled = device.IsEnabled,
                DeviceTypeId = deviceType.Id,
                AdditionalProperties = serializedProps
            };

            _context.Devices.Add(newDevice);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Device with ID {newDevice.Id} added successfully");
            return Created($"api/devices/{newDevice.Id}", $"Device with ID {newDevice.Id} added.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in POST /api/devices: {ex.Message}");
            return Problem(title: "Something went wrong", detail: ex.Message);
        }
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateDevice(int id, [FromBody] DeviceDTO device, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation($"Executing PUT request on api/devices/{id}");

            var deviceToUpdate = await _context.Devices
                .Include(d => d.DeviceEmployees.Where(de => de.ReturnDate == null))
                .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

            if (deviceToUpdate == null)
            {
                _logger.LogInformation($"No device found with id {id}");
                return NotFound("Device not found");
            }

            var username = User.FindFirstValue("sub");
            var role = User.FindFirstValue("role");

            if (role != "Admin")
            {
                var userAccount = await _context.Accounts
                    .Include(a => a.Employee)
                    .FirstOrDefaultAsync(a => a.Username == username, cancellationToken);

                if (userAccount == null || !deviceToUpdate.DeviceEmployees
                    .Any(de => de.EmployeeId == userAccount.Employee.Id))
                {
                    _logger.LogInformation("Forbidden device access");
                    return Forbid();
                }
            }

            var deviceType = await _context.DeviceTypes
                .FirstOrDefaultAsync(dt => dt.Name == device.Type, cancellationToken);

            if (deviceType == null)
            {
                _logger.LogInformation($"No device type found with name '{device.Type}'");
                return NotFound("Device type not found");
            }

            deviceToUpdate.Name = device.Name;
            deviceToUpdate.IsEnabled = device.IsEnabled;
            deviceToUpdate.DeviceTypeId = deviceType.Id;
            deviceToUpdate.AdditionalProperties = JsonSerializer.Serialize(device.AdditionalProperties);

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Device updated successfully");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in PUT /api/devices/{id}: {ex.Message}");
            return Problem(title: "Something went wrong", detail: ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDevice(int id, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation($"Executing DELETE request on api/devices/{id}");

            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

            if (device == null)
            {
                _logger.LogInformation($"No device found with id {id}");
                return NotFound("Device not found");
            }

            _context.Devices.Remove(device);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Device deleted successfully");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in DELETE /api/devices/{id}: {ex.Message}");
            return Problem(title: "Something went wrong", detail: ex.Message);
        }
    }
}
