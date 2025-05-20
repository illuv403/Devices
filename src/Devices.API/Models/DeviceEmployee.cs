using System;
using System.Collections.Generic;

namespace Devices.API;

public partial class DeviceEmployee
{
    public int Id { get; set; }

    public int DeviceId { get; set; }

    public int EmployeeId { get; set; }

    public DateTime IssueDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    public virtual Device Device { get; set; } = null!;

    public virtual Employee Employee { get; set; } = null!;
}
