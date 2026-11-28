namespace GoGoaRentals.Services.Common;

/// <summary>Bound from the "BookingPolicy" configuration section.</summary>
public sealed class BookingPolicyOptions
{
    public const string Section = "BookingPolicy";

    /// <summary>Rental days run from this hour (Goa time) to the same hour the next day.</summary>
    public int RentalDayStartHour { get; set; } = 9;

    public int MaxRentalDays { get; set; } = 30;

    /// <summary>How soon before pickup a booking can be made.</summary>
    public int MinLeadMinutes { get; set; } = 60;

    /// <summary>How far ahead pickup can be.</summary>
    public int MaxAdvanceDays { get; set; } = 180;

    public string Currency { get; set; } = "INR";

    /// <summary>Refundable deposit per booking of any two-wheeler, whatever the number of days.</summary>
    public decimal TwoWheelerDeposit { get; set; } = 1500;

    /// <summary>Refundable deposit per booking of any four-wheeler, whatever the number of days.</summary>
    public decimal FourWheelerDeposit { get; set; } = 4000;

    /// <summary>The fixed deposit for a vehicle category: two-wheeler categories have "two" in their name.</summary>
    public decimal DepositFor(string categoryName)
        => categoryName.Contains("two", StringComparison.OrdinalIgnoreCase) ? TwoWheelerDeposit : FourWheelerDeposit;
}

/// <summary>Bound from the "Allocation" configuration section.</summary>
public sealed class AllocationOptions
{
    public const string Section = "Allocation";

    /// <summary>Gap kept free before and after each reservation of the same vehicle.</summary>
    public int TurnaroundBufferMinutes { get; set; } = 60;

    public int PaymentHoldMinutes { get; set; } = 15;

    public int MaxReservationAttempts { get; set; } = 5;

    public decimal MinPartnerReliabilityScore { get; set; } = 60;
}
