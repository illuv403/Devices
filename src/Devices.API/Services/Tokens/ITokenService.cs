namespace Devices.API.Services.Tokens;

public interface ITokenService
{
    public string GenerateToken(string username, string role);
}