using Microsoft.AspNetCore.Mvc;

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
    
}