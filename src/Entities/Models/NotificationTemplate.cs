using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class NotificationTemplate
{
    public int TemplateId { get; set; }

    public short EventType { get; set; }

    public byte Channel { get; set; }

    public string Language { get; set; } = null!;

    public string? Subject { get; set; }

    public string Body { get; set; } = null!;

    public string? ProviderTemplateName { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
