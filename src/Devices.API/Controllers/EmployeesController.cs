using Devices.API.DTO.EmployeeDTOs;
using Devices.API.DTO.PersonDTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Devices.API.Controllers;

[ApiController]
[Route("api/employees")]
public class EmployeesController
{
    public readonly DevicesDbContext _context;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(DevicesDbContext context, ILogger<EmployeesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IResult> GetAllEmployees(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing GET request on api/employees");
            
            var shortInfoEmployees = await _context.Employees
                .Include(e => e.Person)
                .Select(e => new ShortEmployeeDTO(e.Id, e.Person.FirstName + " " + e.Person.LastName))
                .ToListAsync(cancellationToken);

            _logger.LogInformation($"Retrieved {shortInfoEmployees.Count} employees, execution succeeded");
            return Results.Ok(shortInfoEmployees);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An error occurred while executing GET request on api/employees:\n {ex.Message}");
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
            _logger.LogInformation($"Executing GET request on api/employees/{id}");
            
            var employee = await _context.Employees
                .Include(e => e.Person)
                .Include(e => e.Position)
                .Where(e => e.Id == id)
                .Select(e => new EmployeeByIdDTO(
                    new PersonDTO(
                        e.Person.PassportNumber,
                        e.Person.FirstName,
                        e.Person.MiddleName,
                        e.Person.LastName,
                        e.Person.PhoneNumber,
                        e.Person.Email
                    ),
                    e.Salary,
                    e.Position.Name,
                    e.HireDate.ToString("yyyy-MM-dd")
                ))
                .FirstOrDefaultAsync(cancellationToken);

            if (employee == null)
            {
                _logger.LogInformation($"No employee found with id {id}");
                return Results.NotFound();
            }

            _logger.LogInformation($"Execution succeeded for employee with id {id}");
            return Results.Ok(employee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An error occurred while executing GET request on api/employees/{id}:\n {ex.Message}");
            return Results.Problem(new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Something went wrong",
                Status = StatusCodes.Status500InternalServerError,
                Instance = $"api/employees/{id}"
            });
        }
    }
    
    [HttpPost]
    public async Task<IResult> CreateEmployee([FromBody] CreateEmployeeDTO createEmployeeDto, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing POST request on api/employees");

            var existingPerson = await _context.People
                .FirstOrDefaultAsync(p => p.PassportNumber == createEmployeeDto.Person.PassportNumber, cancellationToken);
            Person person;
            if (existingPerson == null)
            {
                person = new Person
                {
                    PassportNumber = createEmployeeDto.Person.PassportNumber,
                    FirstName = createEmployeeDto.Person.FirstName,
                    MiddleName = createEmployeeDto.Person.MiddleName,
                    LastName = createEmployeeDto.Person.LastName,
                    PhoneNumber = createEmployeeDto.Person.PhoneNumber,
                    Email = createEmployeeDto.Person.Email
                };
                _context.People.Add(person);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                person = existingPerson;
            }

            var position = await _context.Positions
                .FirstOrDefaultAsync(p => p.Id == createEmployeeDto.PositionId, cancellationToken);
            if (position == null)
            {
                _logger.LogInformation($"Position with id {createEmployeeDto.PositionId} not found");
                return Results.BadRequest(new { error = "Invalid PositionId" });
            }

            var employee = new Employee
            {
                PersonId = person.Id,
                PositionId = createEmployeeDto.PositionId,
                Salary = createEmployeeDto.Salary,
                HireDate = DateTime.Now
            };
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Created new employee with id {employee.Id}, execution succeeded");
            return Results.Created($"/api/employees/{employee.Id}", new ShortEmployeeDTO(employee.Id, $"{person.FirstName} {person.LastName}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An error occurred while executing POST request on api/employees:\n {ex.Message}");
            return Results.Problem(new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Something went wrong",
                Status = StatusCodes.Status500InternalServerError,
                Instance = "api/employees"
            });
        }
    }
}