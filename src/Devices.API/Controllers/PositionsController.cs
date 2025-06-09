using Devices.API.DTO.RoleDTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Devices.API.Controllers
{
    [Route("api/positions")]
    [ApiController]
    public class PositionsController : ControllerBase
    {
        public readonly DevicesDbContext _context;
        private readonly ILogger<EmployeesController> _logger;

        public PositionsController(DevicesDbContext context, ILogger<EmployeesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Executing GET request on api/positions");
            
                var positions = await _context.Positions
                    .Select(p => new ShortRolesDTO(
                        p.Id, 
                        p.Name
                        ))
                    .ToListAsync(cancellationToken);

                _logger.LogInformation($"Retrieved {positions.Count} positions, execution succeeded");
                return Ok(positions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while executing GET request on api/positions:\n {ex.Message}");
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
