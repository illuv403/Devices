using System;
using System.Collections.Generic;

namespace Devices.API;

public partial class DeviceType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
}
