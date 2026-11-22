using System.ComponentModel.DataAnnotations;
using GoGoaRentals.Entities.Enums;

namespace GoGoaRentals.Server.WebModels;

public sealed class CreateDailyPriceRequest
{
    [Range(1, int.MaxValue)]
    public int VehicleTypeId { get; set; }

    [Range(1, 1_000_000)]
    public decimal DailyPrice { get; set; }

    [Range(0, 1_000_000)]
    public decimal DepositAmount { get; set; }

    [Range(0, 100)]
    public decimal TaxPercent { get; set; }

    /// <summary>First rental day (Goa date) the price applies to.</summary>
    [Required]
    public DateOnly? FirstDay { get; set; }

    /// <summary>Last rental day it applies to, inclusive. Empty = no end date.</summary>
    public DateOnly? LastDay { get; set; }
}

/// <summary>Times must include an offset (e.g. 2026-11-09T09:00:00+05:30) so there's no doubt about the time zone.</summary>
public sealed class SearchQuery
{
    [Range(1, int.MaxValue)]
    public int PickupLocationId { get; set; }

    public PickupMode PickupMode { get; set; } = PickupMode.SelfPickup;

    [Required]
    public DateTimeOffset? PickupAt { get; set; }

    [Required]
    public DateTimeOffset? ReturnAt { get; set; }
}

public sealed class QuoteRequestModel
{
    [Range(1, int.MaxValue)]
    public int VehicleTypeId { get; set; }

    [Range(1, int.MaxValue)]
    public int PickupLocationId { get; set; }

    public PickupMode PickupMode { get; set; } = PickupMode.SelfPickup;

    [Required]
    public DateTimeOffset? PickupAt { get; set; }

    [Required]
    public DateTimeOffset? ReturnAt { get; set; }
}

public sealed class AddCalendarBlockRequest
{
    [Required]
    public CalendarBlockType? BlockType { get; set; }

    [Required]
    public DateTimeOffset? StartAt { get; set; }

    [Required]
    public DateTimeOffset? EndAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
