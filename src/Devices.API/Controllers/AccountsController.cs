using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Devices.API;
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

    public AccountsController(DevicesDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    // GET: api/Accounts
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<Account>>> GetAccounts(CancellationToken cancellationToken)
    {
        var devices = await _context.Accounts
            .Select(a => new ShortUserDTO(
                a.Id,
                a.Username,
                a.Password
            ))
            .ToListAsync(cancellationToken);
            
        return Ok(devices);
    }

    // GET: api/Accounts/5
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Account>> GetAccount(int id, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .Include(a => a.Employee)
            .ThenInclude(e => e.Person)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (account == null)
        {
            return NotFound();
        }

        var username = User.FindFirstValue("sub");
        var role = User.FindFirstValue("role");
    
        if (role != "Admin" && account.Username != username)
        {
            return Forbid();
        }

        return Ok(new { account.Username, account.Password });
    }

    // PUT: api/Accounts/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> PutAccount(int id,[FromBody] Account account, CancellationToken cancellationToken)
    {
        if (id != account.Id)
        {
            return BadRequest();
        }

        var username = User.FindFirstValue("sub");
        var role = User.FindFirstValue("role");

        if (role != "Admin" && account.Username != username)
        {
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

        return NoContent();
    }

    // POST: api/Accounts
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Account>> PostAccount([FromBody] CreateUserDTO userData, CancellationToken cancellationToken)
    {
        var employee = await _context.Employees
            .Include(e => e.Person)
            .FirstOrDefaultAsync(e => e.Id == userData.EmployeeId, cancellationToken);

        if (employee == null)
        {
            return BadRequest("Employee not found.");
        }

        var account = new Account
        {
            Username = userData.Username,
            Password = _passwordHasher.HashPassword(null, userData.Password),
            // 2 for user 1 for admin
            RoleId = 2, 
            EmployeeId = userData.EmployeeId
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, account);
    }

    // DELETE: api/Accounts/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts.FindAsync(id);
        if (account == null)
        {
            return NotFound();
        }

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private bool AccountExists(int id)
    {
        return _context.Accounts.Any(e => e.Id == id);
    }
}