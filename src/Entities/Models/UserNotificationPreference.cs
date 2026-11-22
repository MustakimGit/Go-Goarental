using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class UserNotificationPreference
{
    public int UserId { get; set; }

    public byte Channel { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime? ConsentGivenAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
