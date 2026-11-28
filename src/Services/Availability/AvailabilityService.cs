using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoGoaRentals.Services.Availability;

public interface IAvailabilityService
{
    /// <summary>
    /// Number of bookable vehicles per vehicle type free for the whole period (with the turnaround buffer).
    /// A partner's pickup location is where the vehicle is based, not a limit: partners bring it to any pickup
    /// point, so every live vehicle counts whichever point the customer picks. Types with no free vehicle are absent.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> CountFreeByTypeAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);
}

public sealed class AvailabilityService(AppDbContext db, IOptions<AllocationOptions> allocation, TimeProvider time) : IAvailabilityService
{
    public async Task<IReadOnlyDictionary<int, int>> CountFreeByTypeAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
    {
        return await BookableVehicles(db)
            .Where(FreeBetween(startUtc, endUtc, allocation.Value.TurnaroundBufferMinutes, time.GetUtcNow().UtcDateTime))
            .GroupBy(v => v.VehicleTypeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
    }

    /// <summary>Vehicles that may be offered to customers at all: live, verified, with an approved partner and an active type.</summary>
    internal static IQueryable<PartnerVehicle> BookableVehicles(AppDbContext db)
    {
        var liveVehicle = EnumValue.ToByte(VehicleStatus.Active);
        var verified = EnumValue.ToByte(DocumentVerificationStatus.Verified);
        var activePartner = EnumValue.ToByte(PartnerStatus.Active);
        var activeType = EnumValue.ToByte(VehicleTypeStatus.Active);

        return db.PartnerVehicles.AsNoTracking().Where(v =>
            v.Status == liveVehicle
            && v.DocumentVerificationStatus == verified
            && v.RentalPartner.PartnerStatus == activePartner
            && v.VehicleType.Status == activeType
            && v.VehicleType.VehicleCategory.IsActive);
    }

    /// <summary>
    /// No blocking calendar entry overlaps [start - buffer, end + buffer). Blocking entries are consumed reservations
    /// and active ones (maintenance, partner time off, or reservations whose payment hold hasn't expired).
    /// </summary>
    internal static System.Linq.Expressions.Expression<Func<PartnerVehicle, bool>> FreeBetween(DateTime startUtc, DateTime endUtc, int bufferMinutes, DateTime nowUtc)
    {
        var from = startUtc.AddMinutes(-bufferMinutes);
        var to = endUtc.AddMinutes(bufferMinutes);
        var active = EnumValue.ToByte(ReservationStatus.Active);
        var consumed = EnumValue.ToByte(ReservationStatus.Consumed);

        return v => !v.VehicleCalendarBlocks.Any(b =>
            (b.Status == consumed || (b.Status == active && (b.ExpiresAt == null || b.ExpiresAt > nowUtc)))
            && b.StartAt < to
            && b.EndAt > from);
    }
}
