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
    public readonly DevicesDbContext _context;

    public DevicesController(DevicesDbContext context)
    {
        _context = context;
    }
    
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllDevices(CancellationToken cancellationToken)
    {
        try
        {
            var devices = await _context.Devices
                .Select(d => new ShortDevicesDTO(
                    d.Id,
                    d.Name
                    ))
                .ToListAsync(cancellationToken);
            
            return Ok(devices);
        }   
        catch (Exception ex)
        {
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
                    DeviceTypeName = d.DeviceType.Name,
                    d.IsEnabled,
                    d.AdditionalProperties,
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
                return NotFound();

            var username = User.FindFirstValue("sub");
            var role = User.FindFirstValue("role");

            if (role != "Admin")
            {
                var userAccount = await _context.Accounts
                    .Include(a => a.Employee)
                    .FirstOrDefaultAsync(a => a.Username == username, cancellationToken);

                if (userAccount == null)
                    return Forbid();

                var isAssigned = await _context.DeviceEmployees
                    .AnyAsync(de => de.DeviceId == id && de.EmployeeId == userAccount.Employee.Id && de.ReturnDate == null, cancellationToken);

                if (!isAssigned)
                    return Forbid();
            }

            var deviceDTO = new DeviceByIdDTO(
                device.Name,
                device.DeviceTypeName,
                device.IsEnabled,
                JsonSerializer.Deserialize<Dictionary<string, string>>(device.AdditionalProperties),
                device.Employee == null ? null : new ShortEmployeeDTO(
                    device.Employee.Id,
                    device.Employee.FullName
                    )
            );
            
            return Ok(deviceDTO);
        }
        catch (Exception ex)
        {
            return Problem(
                detail: ex.Message,
                title: "Something went wrong",
                statusCode: StatusCodes.Status500InternalServerError,
                instance: $"api/devices/{id}"
            );
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddDevice([FromBody] DeviceDTO device, CancellationToken cancellationToken)
    {
        try
        {
            var rowsAffected = -1;
            
            var deviceType = await _context.DeviceTypes.FirstOrDefaultAsync(dt => dt.Name == device.DeviceTypeName, cancellationToken);

            if (deviceType == null)
                return Problem(
                    title: "Device type not found",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: "api/devices"
                );
            
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
                return Created($"api/devices/{newDevice.Id}", $"Device with id {newDevice.Id} was added successfully.");
            }
            else
            {
                return Problem(
                    title: "Error while adding device",
                    statusCode: StatusCodes.Status500InternalServerError,
                    instance: "api/devices"
                );
            }
        }
        catch (Exception ex)
        {
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
            var rowsAffected = -1;
            
            var deviceToUpdate = await _context.Devices
                .Include(d => d.DeviceEmployees
                    .Where(de => de.ReturnDate == null))
                .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            
            if (deviceToUpdate == null)
                return Problem(
                    title: "Device not found",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: "api/devices"
                );

            var username = User.FindFirstValue("sub");
            var role = User.FindFirstValue("role");

            if (role != "Admin")
            {
                var userAccount = await _context.Accounts
                    .Include(a => a.Employee)
                    .FirstOrDefaultAsync(a => a.Username == username, cancellationToken);

                if (userAccount == null)
                    return Forbid();

                var isAssigned = deviceToUpdate.DeviceEmployees
                    .Any(de => de.EmployeeId == userAccount.Employee.Id && de.ReturnDate == null);

                if (!isAssigned)
                    return Forbid();
            }

            var deviceType = await _context.DeviceTypes.FirstOrDefaultAsync(dt => dt.Name == device.DeviceTypeName, cancellationToken);

            if (deviceType == null)
                return Problem(
                    title: "Device type not found",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: "api/devices"
                );
            
            deviceToUpdate.Name = device.Name;
            deviceToUpdate.DeviceTypeId = deviceType.Id;
            deviceToUpdate.IsEnabled = device.IsEnabled;
            deviceToUpdate.AdditionalProperties = JsonSerializer.Serialize(device.AdditionalProperties);
            
            rowsAffected = await _context.SaveChangesAsync(cancellationToken);

            if (rowsAffected != -1)
            {
                return NoContent();
            }
            else
            {
                return Problem(
                    title: "Error while updating device",
                    statusCode: StatusCodes.Status500InternalServerError,
                    instance: "api/devices"
                );
            }
        }
        catch (Exception ex)
        {
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
            var rowsAffected = -1;
            
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            
            if (device == null)
                return Problem(
                    title: "Device not found",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: "api/devices"
                ); 
            
            _context.Devices.Remove(device);
            rowsAffected = await _context.SaveChangesAsync(cancellationToken);

            if (rowsAffected != -1)
            {
                return NoContent();
            }
            else
            {
                return Problem(
                    title: "Error while deleting device",
                    statusCode: StatusCodes.Status500InternalServerError,
                    instance: "api/devices"
                );
            }
        }
        catch (Exception ex)
        {
            return Problem(
                detail: ex.Message,
                title: "Something went wrong",
                statusCode: StatusCodes.Status500InternalServerError,
                instance: "api/devices"
            );
        }
    }
}