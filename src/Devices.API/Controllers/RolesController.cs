using Devices.API.DTO.RoleDTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Devices.API.Controllers
{
    [Route("api/roles")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        public readonly DevicesDbContext _context;
        private readonly ILogger<EmployeesController> _logger;

        public RolesController(DevicesDbContext context, ILogger<EmployeesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Executing GET request on api/roles");
            
                var roles = await _context.Roles
                    .Select(r => new ShortRolesDTO(
                        r.Id, 
                        r.Name
                        ))
                    .ToListAsync(cancellationToken);

                _logger.LogInformation($"Retrieved {roles.Count} roles, execution succeeded");
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while executing GET request on api/roles:\n {ex.Message}");
                return Problem(
                    detail: ex.Message,
                    title: "Something went wrong",
                    statusCode: StatusCodes.Status500InternalServerError,
                    instance: "api/roles"
                );
            }
        }
    }
}
