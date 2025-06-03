using System.ComponentModel.DataAnnotations;

namespace Devices.API.DTO.UserDTOs;

public class LoginUserDTO
{
    [Required]
    public string Username { get; set; }
    
    [Required]
    public string Password { get; set; }
}