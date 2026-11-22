using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoGoaRentals.Services.Pricing;

/// <summary>A daily rate for a vehicle type, applying to rental days FirstDay..LastDay (inclusive; LastDay null = open-ended).</summary>
public sealed record DailyPriceDto(
    int PricingId,
    int VehicleTypeId,
    string VehicleTypeName,
    decimal DailyPrice,
    decimal DepositAmount,
    decimal TaxPercent,
    DateOnly FirstDay,
    DateOnly? LastDay,
    bool IsActive,
    DateTime CreatedAt);

public sealed record CreateDailyPriceCommand(
    int VehicleTypeId,
    decimal DailyPrice,
    decimal DepositAmount,
    decimal TaxPercent,
    DateOnly FirstDay,
    DateOnly? LastDay);

public interface IPricingAdminService
{
    Task<IReadOnlyList<DailyPriceDto>> ListAsync(int? vehicleTypeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a daily rate. If an open-ended rate for the same type started earlier, it is ended the day before,
    /// so "new price from 1 Dec" just works. Any other overlap is refused.
    /// </summary>
    Task<DailyPriceDto> CreateAsync(CreateDailyPriceCommand command, int actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Withdraws a rate. Bookings already made keep the price they were quoted.</summary>
    Task<DailyPriceDto> DeactivateAsync(int pricingId, int actorUserId, CancellationToken cancellationToken = default);
}

public sealed class PricingAdminService(AppDbContext db, IOptions<BookingPolicyOptions> policy) : IPricingAdminService
{
    private static readonly byte Daily = EnumValue.ToByte(DurationType.Daily);

    private int StartHour => policy.Value.RentalDayStartHour;

    public async Task<IReadOnlyList<DailyPriceDto>> ListAsync(int? vehicleTypeId, CancellationToken cancellationToken = default)
    {
        var rows = await db.VehiclePricings.AsNoTracking()
            .Include(p => p.VehicleType)
            .Where(p => p.DurationType == Daily && (vehicleTypeId == null || p.VehicleTypeId == vehicleTypeId))
            .OrderBy(p => p.VehicleType.Name).ThenByDescending(p => p.EffectiveFrom)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<DailyPriceDto> CreateAsync(CreateDailyPriceCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        if (command.DailyPrice <= 0) throw new BusinessRuleException("The daily price must be more than zero.");
        if (command.DepositAmount < 0) throw new BusinessRuleException("The deposit can't be negative.");
        if (command.TaxPercent is < 0 or > 100) throw new BusinessRuleException("Tax must be between 0 and 100 percent.");
        if (command.LastDay is { } last && last < command.FirstDay) throw new BusinessRuleException("The last day can't be before the first day.");

        if (!await db.VehicleTypes.AnyAsync(t => t.VehicleTypeId == command.VehicleTypeId, cancellationToken))
        {
            throw new NotFoundException("Vehicle type");
        }

        var from = RentalDays.DayStartUtc(command.FirstDay, StartHour);
        DateTime? to = command.LastDay is { } lastDay ? RentalDays.DayStartUtc(lastDay.AddDays(1), StartHour) : null;

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var overlapping = await db.VehiclePricings
            .Where(p => p.VehicleTypeId == command.VehicleTypeId && p.DurationType == Daily && p.IsActive)
            .Where(p => (to == null || p.EffectiveFrom < to) && (p.EffectiveTo == null || p.EffectiveTo > from))
            .ToListAsync(cancellationToken);

        foreach (var existing in overlapping)
        {
            if (existing.EffectiveFrom < from)
            {
                // The new rate cuts into a rate that started earlier: end that one where the new one starts and,
                // if it ran past the new rate (e.g. a base rate around a peak-season rate), resume it afterwards.
                var resumeUntil = existing.EffectiveTo;
                existing.EffectiveTo = from;
                AuditWriter.Add(db, actorUserId, "VehiclePriceEnded", nameof(VehiclePricing), existing.PricingId,
                    new { LastDay = command.FirstDay.AddDays(-1) });

                if (to is { } newEnd && (resumeUntil is null || resumeUntil > newEnd))
                {
                    db.VehiclePricings.Add(new VehiclePricing
                    {
                        VehicleTypeId = existing.VehicleTypeId,
                        DurationType = Daily,
                        CustomerPrice = existing.CustomerPrice,
                        DepositAmount = existing.DepositAmount,
                        TaxPercent = existing.TaxPercent,
                        EffectiveFrom = newEnd,
                        EffectiveTo = resumeUntil,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                    });
                }

                continue;
            }

            var existingFirst = RentalDays.DayOf(existing.EffectiveFrom, StartHour);
            var existingLast = existing.EffectiveTo is { } end ? RentalDays.DayOf(end, StartHour).AddDays(-1) : (DateOnly?)null;
            throw new ConflictException(
                $"This overlaps the ₹{existing.CustomerPrice:0.##}/day rate from {existingFirst:d MMM yyyy}" +
                (existingLast is { } l ? $" to {l:d MMM yyyy}" : " onwards") + ". Change the dates or withdraw that rate first.");
        }

        var price = new VehiclePricing
        {
            VehicleTypeId = command.VehicleTypeId,
            DurationType = Daily,
            CustomerPrice = command.DailyPrice,
            DepositAmount = command.DepositAmount,
            TaxPercent = command.TaxPercent,
            EffectiveFrom = from,
            EffectiveTo = to,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.VehiclePricings.Add(price);
        await db.SaveChangesAsync(cancellationToken);

        AuditWriter.Add(db, actorUserId, "VehiclePriceCreated", nameof(VehiclePricing), price.PricingId, command);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (await ListAsync(command.VehicleTypeId, cancellationToken)).Single(p => p.PricingId == price.PricingId);
    }

    public async Task<DailyPriceDto> DeactivateAsync(int pricingId, int actorUserId, CancellationToken cancellationToken = default)
    {
        var price = await db.VehiclePricings.Include(p => p.VehicleType)
            .SingleOrDefaultAsync(p => p.PricingId == pricingId && p.DurationType == Daily, cancellationToken)
            ?? throw new NotFoundException("Price");

        if (price.IsActive)
        {
            price.IsActive = false;
            AuditWriter.Add(db, actorUserId, "VehiclePriceWithdrawn", nameof(VehiclePricing), pricingId);
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToDto(price);
    }

    private DailyPriceDto ToDto(VehiclePricing p) => new(
        p.PricingId,
        p.VehicleTypeId,
        p.VehicleType.Name,
        p.CustomerPrice,
        p.DepositAmount,
        p.TaxPercent,
        RentalDays.DayOf(p.EffectiveFrom, StartHour),
        p.EffectiveTo is { } to ? RentalDays.DayOf(to, StartHour).AddDays(-1) : null,
        p.IsActive,
        p.CreatedAt);
}
