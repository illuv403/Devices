using Devices.API.DTO.TokenDTOs;
using Devices.API.DTO.UserDTOs;
using Devices.API.Services.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Devices.API.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly DevicesDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly PasswordHasher<Account> _passwordHasher = new();

    public AuthController(DevicesDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    [HttpPost]
    public async Task<IActionResult> Auth(LoginUserDTO loginData, CancellationToken cancellationToken)
    {
        var foundAccount = await _context.Accounts.Include(a => a.Role).FirstOrDefaultAsync(a => string.Equals(a.Username, loginData.Username), cancellationToken);

        if (foundAccount == null)
        {
            return Unauthorized();
        }
        
        var verificationResult = _passwordHasher.VerifyHashedPassword(foundAccount, foundAccount.Password, loginData.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        var token = new TokenDTO
        {
            Token = _tokenService.GenerateToken(foundAccount.Username, foundAccount.Role.Name)
        };
        
        return Ok(token);
    }
    
    [HttpPost("hash-password")]
    [AllowAnonymous]
    public IActionResult HashPassword([FromBody] string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return BadRequest("Password cannot be empty.");
        }

        var hashedPassword = _passwordHasher.HashPassword(null, password);
        return Ok(new { HashedPassword = hashedPassword });
    }
}