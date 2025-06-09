using System.Text.Json;
using Devices.API.DTO.DeviceDTOs;
using Devices.API.DTO.EmployeeDTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.CodeAnalysis.Elfie.Serialization;

namespace Devices.API.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    public readonly DevicesDbContext _context;
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
            _logger.LogError(ex, $"An error occured while executing GET request on api/devices:\n {ex.Message}");
            return Problem(
                detail: ex.Message,
                title: "Something went wrong",
                statusCode: StatusCodes.Status500InternalServerError,
                instance: "api/devices"
            );
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
                .Include(d => d.DeviceEmployees
                    .Where(de => de.ReturnDate == null))
                .ThenInclude(de => de.Employee)
                .ThenInclude(e => e.Person)
                .Where(d => d.Id == id)
                .Select(d => new
                {
                    d.Name,
                    IsEnabled = d.IsEnabled,
                    AdditionalProperties = d.AdditionalProperties,
                    TypeId = d.DeviceType.Id,
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
                    _logger.LogInformation($"No devices assigned");
                    return Forbid();
                }
            }

            var deviceDTO = new DeviceByIdDTO(
                device.Name,
                device.IsEnabled,
                JsonSerializer.Deserialize<Dictionary<string, string>>(device.AdditionalProperties),
                device.TypeId
            );

            _logger.LogInformation($"Execution succeded");
            return Ok(deviceDTO);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An error occured while executing GET request on api/devices:\n {ex.Message}");
            return Problem(
                detail: ex.Message,
                title: "Something went wrong",
                statusCode: StatusCodes.Status500InternalServerError,
                instance: $"api/devices/{id}"
            );
        }
    }

    [HttpGet("types")]
    public async Task<IActionResult> GetDevicesTypes(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing GET request on api/devices");

            var devicesTypes = await _context.DeviceTypes
                .Select(dt => new DeviceTypesDTO(
                    dt.Id, 
                    dt.Name
                    ))
                .ToListAsync(cancellationToken);
            
            _logger.LogInformation($"Retrieved {devicesTypes.Count} device types, execution succeeded");
            return Ok(devicesTypes);
        }   
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An error occured while executing GET request on api/devices/types:\n {ex.Message}");
            return Problem(
                detail: ex.Message,
                title: "Something went wrong",
                statusCode: StatusCodes.Status500InternalServerError,
                instance: $"api/devices/types"
            );
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddDevice([FromBody] DeviceDTO device, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation($"Executing POST request on api/devices");
            
            var rowsAffected = -1;
            
            var deviceType = await _context.DeviceTypes.FirstOrDefaultAsync(dt => dt.Id == device.TypeId, cancellationToken);

            if (deviceType == null)
            {
                _logger.LogInformation($"No device type found with id {device.TypeId}");
                return Problem(
                    title: "Device type not found",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: "api/devices"
                );
            }

            var AdditionalProperties = JsonSerializer.Serialize(device.AdditionalProperties);

            var newDevice = new Device
            {
                Name = device.Name,
                DeviceTypeId = deviceType.Id,
                IsEnabled = device.IsEnabled,
                AdditionalProperties = AdditionalProperties
            };
            _context.Devices.Add(newDevice);
            rowsAffected = await _context.SaveChangesAsync(cancellationToken);

            if (rowsAffected != -1)
            {
                _logger.LogInformation($"Execution succeded");
                return Created($"api/devices/{newDevice.Id}", $"Device with id {newDevice.Id} was added successfully.");
            }
            else
            {
                _logger.LogInformation($"Execution failed");
                return Problem(
                    title: "Error while adding device",
                    statusCode: StatusCodes.Status500InternalServerError,
                    instance: "api/devices"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An error occured while executing POST request on api/devices:\n {ex.Message}");
            return Problem(
                detail: ex.Message,
                title: "Something went wrong",
                statusCode: StatusCodes.Status500InternalServerError,
                instance: "api/devices"
            );
        }
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateDevice(int id, [FromBody] DeviceDTO device, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation($"Executing PUT request on api/devices/{id}");
            
            var rowsAffected = -1;
            
            var deviceToUpdate = await _context.Devices
                .Include(d => d.DeviceEmployees
                    .Where(de => de.ReturnDate == null))
                .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

            if (deviceToUpdate == null)
            {
                _logger.LogInformation($"No device found with id {id}");
                return Problem(
                    title: "Device not found",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: "api/devices"
                );
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

                var isAssigned = deviceToUpdate.DeviceEmployees
                    .Any(de => de.EmployeeId == userAccount.Employee.Id && de.ReturnDate == null);
                
                if (!isAssigned)
                {
                    _logger.LogInformation($"No devices assigned");
                    return Forbid();
                }
            }

            var deviceType = await _context.DeviceTypes.FirstOrDefaultAsync(dt => dt.Id == device.TypeId, cancellationToken);

            if (deviceType == null)
            {
                _logger.LogInformation($"No device type found with id {id}");
                return Problem(
                    title: "Device type not found",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: "api/devices"
                );
            }

            deviceToUpdate.Name = device.Name;
            deviceToUpdate.DeviceTypeId = deviceType.Id;
            deviceToUpdate.IsEnabled = device.IsEnabled;
            deviceToUpdate.AdditionalProperties = JsonSerializer.Serialize(device.AdditionalProperties);
            
            rowsAffected = await _context.SaveChangesAsync(cancellationToken);

            if (rowsAffected != -1)
            {
                _logger.LogInformation($"Execution succeded");
                return NoContent();
            }
            else
            {
                _logger.LogInformation($"Execution failed");
                return Problem(
                    title: "Error while updating device",
                    statusCode: StatusCodes.Status500InternalServerError,
                    instance: "api/devices"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An error occured while executing PUT request on api/devices/{id}:\n {ex.Message}");
            return Problem(
                detail: ex.Message,
                title: "Something went wrong",
                statusCode: StatusCodes.Status500InternalServerError,
                instance: "api/devices"
            );
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDevice(int id, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation($"Executing DELETE request on api/devices/{id}");
            
            var rowsAffected = -1;
            
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

            if (device == null)
            {
                _logger.LogInformation($"No device found with id {id}");
                return Problem(
                    title: "Device not found",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: "api/devices"
                );
            }

            _context.Devices.Remove(device);
            rowsAffected = await _context.SaveChangesAsync(cancellationToken);

            if (rowsAffected != -1)
            {
                _logger.LogInformation($"Execution succeded");
                return NoContent();
            }
            else
            {
                _logger.LogInformation($"Execution failed");
                return Problem(
                    title: "Error while deleting device",
                    statusCode: StatusCodes.Status500InternalServerError,
                    instance: "api/devices"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An error occured while executing DELETE request on api/devices/{id}:\n {ex.Message}");
            return Problem(
                detail: ex.Message,
                title: "Something went wrong",
                statusCode: StatusCodes.Status500InternalServerError,
                instance: "api/devices"
            );
        }
    }
}