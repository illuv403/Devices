using System.ComponentModel.DataAnnotations;

namespace Devices.API.DTO.UserDTOs;

public class CreateUserDTO
{
    [Required]
    [RegularExpression(@"^[^\d].*$")]
    public string Username { get; set; }
    
    [Required]
    [RegularExpression("\"^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$%^&*-]).{12,}$\"")]
    public string Password { get; set; }

    public int EmployeeId { get; set; }
    
    public int RoleId { get; set; }
}