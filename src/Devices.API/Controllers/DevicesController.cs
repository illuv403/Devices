using System.Text.Json;
using Devices.API.DTO.DeviceDTOs;
using Devices.API.DTO.EmployeeDTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Devices.API.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController
{
    public readonly DevicesDbContext _context;

    public DevicesController(DevicesDbContext context)
    {
        _context = context;
    }
    
    [HttpGet]
    public async Task<IResult> GetAllDevices(CancellationToken cancellationToken)
    {
        try
        {
            var devices = await _context.Devices
                .Select(d => new ShortDevicesDTO(
                    d.Id,
                    d.Name
                    ))
                .ToListAsync(cancellationToken);
            
            return Results.Ok(devices);
        }   
        catch (Exception ex)
        {
            return Results.Problem(new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Something went wrong",
                Status = StatusCodes.Status500InternalServerError,
                Instance = "api/devices"
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<IResult> GetDeviceById(int id, CancellationToken cancellationToken)
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
                return Results.NotFound();

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
            
            return Results.Ok(deviceDTO);
        }
        catch (Exception ex)
        {
            return Results.Problem(new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Something went wrong",
                Status = StatusCodes.Status500InternalServerError,
                Instance = $"api/devices/{id}"
            });
        }
    }

    [HttpPost]
    public async Task<IResult> AddDevice([FromBody] DeviceDTO device, CancellationToken cancellationToken)
    {
        try
        {
            var rowsAffected = -1;
            
            var deviceType = await _context.DeviceTypes.FirstOrDefaultAsync(dt => dt.Name == device.DeviceTypeName, cancellationToken);

            if (deviceType == null)
                return Results.Problem(new ProblemDetails
                {
                    Title = "Device type not found",
                    Status = StatusCodes.Status404NotFound,
                    Instance = "api/devices"
                });
            
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
                return Results.Created($"api/devices/{newDevice.Id}", $"Device with id {newDevice.Id} was added successfully.");
            }
            else
            {
                return Results.Problem(new ProblemDetails
                {
                    Title = "Error while adding device",
                    Status = StatusCodes.Status500InternalServerError,
                    Instance = "api/devices"
                });
            }
        }
        catch (Exception ex)
        {
            return Results.Problem(new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Something went wrong",
                Status = StatusCodes.Status500InternalServerError,
                Instance = "api/devices"
            });
        }
    }

    [HttpPut("{id}")]
    public async Task<IResult> UpdateDevice(int id, [FromBody] DeviceDTO device, CancellationToken cancellationToken)
    {
        try
        {
            var rowsAffected = -1;
            
            var deviceToUpdate = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            
            if (deviceToUpdate == null)
                return Results.Problem(new ProblemDetails
                {
                    Title = "Device not found",
                    Status = StatusCodes.Status404NotFound,
                    Instance = "api/devices"
                });
            
            var deviceType = await _context.DeviceTypes.FirstOrDefaultAsync(dt => dt.Name == device.DeviceTypeName, cancellationToken);

            if (deviceType == null)
                return Results.Problem(new ProblemDetails
                {
                    Title = "Device type not found",
                    Status = StatusCodes.Status404NotFound,
                    Instance = "api/devices"
                });
            
            deviceToUpdate.Name = device.Name;
            deviceToUpdate.DeviceTypeId = deviceType.Id;
            deviceToUpdate.IsEnabled = device.IsEnabled;
            deviceToUpdate.AdditionalProperties = JsonSerializer.Serialize(device.AdditionalProperties);
            
            rowsAffected = await _context.SaveChangesAsync(cancellationToken);

            if (rowsAffected != -1)
            {
                return Results.NoContent();
            }
            else
            {
                return Results.Problem(new ProblemDetails
                {
                    Title = "Error while updating device",
                    Status = StatusCodes.Status500InternalServerError,
                    Instance = "api/devices"
                });
            }
        }
        catch (Exception ex)
        {
            return Results.Problem(new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Something went wrong",
                Status = StatusCodes.Status500InternalServerError,
                Instance = "api/devices"
            });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IResult> DeleteDevice(int id, CancellationToken cancellationToken)
    {
        try
        {
            var rowsAffected = -1;
            
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            
            if (device == null)
                return Results.Problem(new ProblemDetails
                {
                    Title = "Device not found",
                    Status = StatusCodes.Status404NotFound,
                    Instance = "api/devices"
                }); 
            
            _context.Devices.Remove(device);
            rowsAffected = await _context.SaveChangesAsync(cancellationToken);

            if (rowsAffected != -1)
            {
                return Results.NoContent();
            }
            else
            {
                return Results.Problem(new ProblemDetails
                {
                    Title = "Error while deleting device",
                    Status = StatusCodes.Status500InternalServerError,
                    Instance = "api/devices"
                });
            }
        }
        catch (Exception ex)
        {
            return Results.Problem(new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Something went wrong",
                Status = StatusCodes.Status500InternalServerError,
                Instance = "api/devices"
            });
        }
    }
    
}