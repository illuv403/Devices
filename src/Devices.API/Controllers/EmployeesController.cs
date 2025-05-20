using Devices.API.DTO.EmployeeDTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Devices.API.Controllers;

[ApiController]
[Route("api/employees")]
public class EmployeesController
{
    public readonly DevicesDbContext _context;

    public EmployeesController(DevicesDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IResult> GetAllEmployees(CancellationToken cancellationToken)
    {
        try
        {
            var shortInfoEmployees = await _context.Employees
                .Include(e => e.Person)
                .Select(e => new ShortEmployeeDTO(e.Id, e.Person.FirstName + " " + e.Person.LastName))
                .ToListAsync(cancellationToken);

            return Results.Ok(shortInfoEmployees);
        }
        catch (Exception ex)
        {
            return Results.Problem(new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Something went wrong",
                Status = StatusCodes.Status500InternalServerError,
                Instance = "api/employees"
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<IResult> GetEmployeeById(int id, CancellationToken cancellationToken)
    {
        try
        {
            var employee = await _context.Employees
                .Include(e => e.Person)
                .Include(e => e.Position)
                .Where(e => e.Id == id)
                .Select(e => new EmployeeByIdDTO(
                    e.Person.PassportNumber,
                    e.Person.FirstName,
                    e.Person.MiddleName ?? string.Empty,
                    e.Person.LastName,
                    e.Person.PhoneNumber,
                    e.Person.Email,
                    e.Salary,
                    new PositionDTO(e.Position.Id, e.Position.Name),
                    e.HireDate
                ))
                .FirstOrDefaultAsync(cancellationToken);

            if (employee == null)
                return Results.NotFound();

            return Results.Ok(employee);
        }
        catch (Exception ex)
        {
            return Results.Problem(new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Something went wrong",
                Status = StatusCodes.Status500InternalServerError,
                Instance = $"api/employees/{id}"
            });
        }
    }
}