using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Devices.API;
using Devices.API.DTO.AccountDTOs;
using Devices.API.DTO.UserDTOs;
using Devices.API.Services.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Devices.API.Controllers;

[Route("api/accounts")]
[ApiController]
public class AccountsController : ControllerBase
{
    private readonly DevicesDbContext _context;
    private readonly PasswordHasher<Account> _passwordHasher = new();
    private readonly ITokenService _tokenService;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(DevicesDbContext context, ITokenService tokenService, ILogger<AccountsController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    // GET: api/Accounts
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<Account>>> GetAccounts(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing GET request on api/accounts");
        
        var accounts = await _context.Accounts
            .Select(a => new ShortUserDTO(
                a.Id,
                a.Username,
                a.Password
            ))
            .ToListAsync(cancellationToken);
            
        _logger.LogInformation($"Retrieved {accounts.Count} accounts, execution succeeded");
        return Ok(accounts);
    }

    // GET: api/Accounts/5
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Account>> GetAccount(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Executing GET request on api/accounts/{id}");
        
        var account = await _context.Accounts
            .Include(a => a.Employee)
            .ThenInclude(e => e.Person)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (account == null)
        {
            _logger.LogInformation($"No account found with id {id}");
            return NotFound();
        }

        var username = User.FindFirstValue("sub");
        var role = User.FindFirstValue("role");
    
        if (role != "Admin" && account.Username != username)
        {
            _logger.LogInformation($"User {username} has role {role}, it is forbidden to get account");
            return Forbid();
        }
        
        var accountDTO = new AccountDTO(account.Username, account.Role.Name);

        _logger.LogInformation($"Execution succeeded for account {id}");
        return Ok(accountDTO);
    }

    // PUT: api/Accounts/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> PutAccount(int id,[FromBody] Account account, CancellationToken cancellationToken)
    { 
        _logger.LogInformation($"Executing PUT request on api/accounts/{id}");
        
        if (id != account.Id)
        {
            _logger.LogInformation($"Account with id {id} does not match");
            return BadRequest();
        }

        var username = User.FindFirstValue("sub");
        var role = User.FindFirstValue("role");

        if (role != "Admin" && account.Username != username)
        {
            _logger.LogInformation($"User {username} has role {role}, it is forbidden to change account details");
            return Forbid();
        }

        _context.Entry(account).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!AccountExists(id)) return NotFound();
            throw;
        }

        _logger.LogInformation($"Execution succeeded for account {id}");
        return NoContent();
    }

    // POST: api/Accounts
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Account>> PostAccount([FromBody] CreateUserDTO userData, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Executing POST request on api/accounts");

        var employee = await _context.Employees
            .Include(e => e.Person)
            .FirstOrDefaultAsync(e => e.Id == userData.EmployeeId, cancellationToken);

        if (employee == null)
        {
            _logger.LogInformation($"No employee found with id {userData.EmployeeId}");
            return BadRequest("Employee not found.");
        }

        var account = new Account
        {
            Username = userData.Username,
            Password = _passwordHasher.HashPassword(null, userData.Password),
            RoleId = userData.RoleId, 
            EmployeeId = userData.EmployeeId
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Execution succeeded for account {userData.EmployeeId}");
        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, account);
    }

    // DELETE: api/Accounts/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Executing DELETE request on api/accounts/{id}");
        
        var account = await _context.Accounts.FindAsync(id);
        if (account == null)
        {
            _logger.LogInformation($"No account found with id {id}");
            return NotFound();
        }

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Execution succeeded for account {id}");
        return NoContent();
    }

    private bool AccountExists(int id)
    {
        return _context.Accounts.Any(e => e.Id == id);
    }
}