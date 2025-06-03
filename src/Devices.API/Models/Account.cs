using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Devices.API;

public partial class Account
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;
    
    public string Password { get; set; } = null!;

    public int EmployeeId { get; set; }

    public int RoleId { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;
}
