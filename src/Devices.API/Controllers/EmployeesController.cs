using Microsoft.AspNetCore.Mvc;

namespace Devices.API.Controllers;

[ApiController]
[Route("api/employees")]
public class EmployeesController
{
    [HttpGet]
    public IResult GetAllEmployees()
    {
        return Results.Ok();
    }
}