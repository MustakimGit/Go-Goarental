using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Availability;
using GoGoaRentals.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoGoaRentals.Services.Pricing;

public sealed record SearchRequest(int PickupLocationId, PickupMode PickupMode, DateTimeOffset PickupAt, DateTimeOffset ReturnAt);

public sealed record QuoteRequest(int VehicleTypeId, int PickupLocationId, PickupMode PickupMode, DateTimeOffset PickupAt, DateTimeOffset ReturnAt);

public sealed record QuoteDay(DateOnly Day, int PricingId, decimal Price, decimal TaxPercent);

/// <summary>
/// A fixed price for one vehicle type and period. In Phase 4 the booking stores this whole object as its
/// pricing snapshot, so later price changes never affect a booking already made.
/// </summary>
public sealed record Quote(
    int VehicleTypeId,
    string VehicleTypeName,
    string CategoryName,
    string? Description,
    byte? SeatingCapacity,
    string? ImageUrl,
    int PickupLocationId,
    string PickupLocationName,
    PickupMode PickupMode,
    DateTime PickupAtUtc,
    DateTime ReturnAtUtc,
    int RentalDays,
    IReadOnlyList<QuoteDay> Days,
    decimal RentalPrice,
    decimal DeliveryFee,
    decimal TaxAmount,
    decimal DepositAmount,
    decimal TotalAmount,
    string Currency,
    int AvailableVehicles);

public interface IQuoteService
{
    /// <summary>Quotes every vehicle type that has a price for the whole period, cheapest first, with availability.</summary>
    Task<IReadOnlyList<Quote>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

    Task<Quote> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken = default);
}

public sealed class QuoteService(
    AppDbContext db,
    IAvailabilityService availability,
    IOptions<BookingPolicyOptions> policy,
    TimeProvider time) : IQuoteService
{
    private static readonly byte Daily = EnumValue.ToByte(DurationType.Daily);
    private static readonly byte ActiveType = EnumValue.ToByte(VehicleTypeStatus.Active);

    public async Task<IReadOnlyList<Quote>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var period = ValidatePeriod(request.PickupAt, request.ReturnAt);
        var location = await LoadLocationAsync(request.PickupLocationId, cancellationToken);
        var types = await db.VehicleTypes.AsNoTracking()
            .Include(t => t.VehicleCategory)
            .Where(t => t.Status == ActiveType && t.VehicleCategory.IsActive)
            .ToListAsync(cancellationToken);
        var prices = await LoadPricesAsync(types.Select(t => t.VehicleTypeId).ToList(), period, cancellationToken);
        var free = await availability.CountFreeByTypeAsync(location.PickupLocationId, period.StartUtc, period.EndUtc, cancellationToken);

        return types
            .Select(t => BuildQuote(t, location, request.PickupMode, period, prices, free))
            .OfType<Quote>()
            .OrderBy(q => q.AvailableVehicles == 0)
            .ThenBy(q => q.TotalAmount)
            .ToList();
    }

    public async Task<Quote> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken = default)
    {
        var period = ValidatePeriod(request.PickupAt, request.ReturnAt);
        var location = await LoadLocationAsync(request.PickupLocationId, cancellationToken);
        var type = await db.VehicleTypes.AsNoTracking()
            .Include(t => t.VehicleCategory)
            .SingleOrDefaultAsync(t => t.VehicleTypeId == request.VehicleTypeId && t.Status == ActiveType && t.VehicleCategory.IsActive, cancellationToken)
            ?? throw new NotFoundException("Vehicle type");
        var prices = await LoadPricesAsync([type.VehicleTypeId], period, cancellationToken);
        var free = await availability.CountFreeByTypeAsync(location.PickupLocationId, period.StartUtc, period.EndUtc, cancellationToken);

        return BuildQuote(type, location, request.PickupMode, period, prices, free)
               ?? throw new BusinessRuleException($"{type.Name} isn't available to book for these dates.");
    }

    private sealed record Period(DateTime StartUtc, DateTime EndUtc, IReadOnlyList<DateOnly> Days);

    private Period ValidatePeriod(DateTimeOffset pickupAt, DateTimeOffset returnAt)
    {
        var options = policy.Value;
        var now = time.GetUtcNow();
        if (returnAt <= pickupAt)
        {
            throw new BusinessRuleException("Return must be after pickup.");
        }

        if (pickupAt < now.AddMinutes(options.MinLeadMinutes))
        {
            throw new BusinessRuleException($"Pickup must be at least {options.MinLeadMinutes} minutes from now.");
        }

        if (pickupAt > now.AddDays(options.MaxAdvanceDays))
        {
            throw new BusinessRuleException($"Bookings open {options.MaxAdvanceDays} days ahead.");
        }

        // Whole minutes only; the database stores datetime2(0).
        var start = GoaClock.TruncateToMinute(pickupAt.UtcDateTime);
        var end = GoaClock.TruncateToMinute(returnAt.UtcDateTime);
        var days = RentalDays.Between(start, end, options.RentalDayStartHour);
        if (days.Count > options.MaxRentalDays)
        {
            throw new BusinessRuleException($"Rentals can be at most {options.MaxRentalDays} days.");
        }

        return new Period(start, end, days);
    }

    private async Task<PickupLocation> LoadLocationAsync(int pickupLocationId, CancellationToken cancellationToken)
        => await db.PickupLocations.AsNoTracking()
               .SingleOrDefaultAsync(l => l.PickupLocationId == pickupLocationId && l.IsActive, cancellationToken)
           ?? throw new NotFoundException("Pickup location");

    /// <summary>Active daily rates for the given types that cover any day of the period.</summary>
    private async Task<ILookup<int, VehiclePricing>> LoadPricesAsync(List<int> typeIds, Period period, CancellationToken cancellationToken)
    {
        var startHour = policy.Value.RentalDayStartHour;
        var firstDayStart = RentalDays.DayStartUtc(period.Days[0], startHour);
        var lastDayStart = RentalDays.DayStartUtc(period.Days[^1], startHour);
        var rows = await db.VehiclePricings.AsNoTracking()
            .Where(p => typeIds.Contains(p.VehicleTypeId) && p.DurationType == Daily && p.IsActive)
            .Where(p => p.EffectiveFrom <= lastDayStart && (p.EffectiveTo == null || p.EffectiveTo > firstDayStart))
            .ToListAsync(cancellationToken);
        return rows.ToLookup(p => p.VehicleTypeId);
    }

    /// <summary>Null when some day of the period has no price for this type.</summary>
    private Quote? BuildQuote(VehicleType type, PickupLocation location, PickupMode mode, Period period,
        ILookup<int, VehiclePricing> prices, IReadOnlyDictionary<int, int> free)
    {
        var startHour = policy.Value.RentalDayStartHour;
        var days = new List<QuoteDay>(period.Days.Count);
        foreach (var day in period.Days)
        {
            // Each rental day is priced at the rate in effect when that day starts, so seasonal rates apply per day.
            var dayStart = RentalDays.DayStartUtc(day, startHour);
            var rate = prices[type.VehicleTypeId]
                .Where(p => p.EffectiveFrom <= dayStart && (p.EffectiveTo is null || p.EffectiveTo > dayStart))
                .MaxBy(p => p.EffectiveFrom);
            if (rate is null)
            {
                return null;
            }

            days.Add(new QuoteDay(day, rate.PricingId, rate.CustomerPrice, rate.TaxPercent));
        }

        var rental = days.Sum(d => d.Price);
        var tax = Money(days.Sum(d => d.Price * d.TaxPercent / 100m));
        var deliveryFee = mode == PickupMode.Delivery ? location.CustomerDeliveryFee : 0m;
        // The deposit follows the rate on the first day.
        var deposit = prices[type.VehicleTypeId].Single(p => p.PricingId == days[0].PricingId).DepositAmount;

        return new Quote(
            type.VehicleTypeId,
            type.Name,
            type.VehicleCategory.Name,
            type.Description,
            type.SeatingCapacity,
            type.ImageUrl,
            location.PickupLocationId,
            location.Name,
            mode,
            period.StartUtc,
            period.EndUtc,
            days.Count,
            days,
            rental,
            deliveryFee,
            tax,
            deposit,
            rental + deliveryFee + tax + deposit,
            policy.Value.Currency,
            free.GetValueOrDefault(type.VehicleTypeId));
    }

    private static decimal Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
