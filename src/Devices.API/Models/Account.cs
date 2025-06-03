using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Devices.API;

public partial class Account
{
    public int Id { get; set; }

    [Required]
    public string Username { get; set; } = null!;

    [Required]
    [RegularExpression("^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$%^&*-]).{12,}$")]
    public string Password { get; set; } = null!;

    public int EmployeeId { get; set; }

    public int RoleId { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;
}
