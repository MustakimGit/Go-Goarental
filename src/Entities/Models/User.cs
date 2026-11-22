using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = null!;

    public byte Role { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    public virtual RentalPartner? RentalPartner { get; set; }

    public virtual Role RoleNavigation { get; set; } = null!;

    public virtual ICollection<UserNotificationPreference> UserNotificationPreferences { get; set; } = new List<UserNotificationPreference>();

    public virtual ICollection<VehicleCalendarBlock> VehicleCalendarBlocks { get; set; } = new List<VehicleCalendarBlock>();

    public virtual ICollection<VehicleDocument> VehicleDocuments { get; set; } = new List<VehicleDocument>();
}
