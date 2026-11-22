using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoGoaRentals.Services.Fleet;

public sealed record CalendarBlockDto(
    int CalendarBlockId,
    int PartnerVehicleId,
    CalendarBlockType BlockType,
    DateTime StartAt,
    DateTime EndAt,
    string? Notes,
    string? BookingNumber,
    DateTime CreatedAt);

public sealed record AddCalendarBlockCommand(CalendarBlockType BlockType, DateTimeOffset StartAt, DateTimeOffset EndAt, string? Notes);

/// <summary>A partner's view of one vehicle's calendar: upcoming bookings and the time they've blocked off.</summary>
public interface IVehicleCalendarService
{
    Task<IReadOnlyList<CalendarBlockDto>> ListUpcomingAsync(int rentalPartnerId, int vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Blocks the vehicle for maintenance or partner use. Refused if it overlaps a customer reservation.</summary>
    Task<CalendarBlockDto> AddBlockAsync(int rentalPartnerId, int vehicleId, AddCalendarBlockCommand command, int actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Removes a maintenance or time-off block. Reservations can't be removed here.</summary>
    Task RemoveBlockAsync(int rentalPartnerId, int calendarBlockId, int actorUserId, CancellationToken cancellationToken = default);
}

public sealed class VehicleCalendarService(AppDbContext db, IOptions<AllocationOptions> allocation, TimeProvider time) : IVehicleCalendarService
{
    private static readonly byte Active = EnumValue.ToByte(ReservationStatus.Active);
    private static readonly byte Consumed = EnumValue.ToByte(ReservationStatus.Consumed);
    private static readonly byte Reservation = EnumValue.ToByte(CalendarBlockType.Reservation);

    public async Task<IReadOnlyList<CalendarBlockDto>> ListUpcomingAsync(int rentalPartnerId, int vehicleId, CancellationToken cancellationToken = default)
    {
        await EnsureOwnedAsync(rentalPartnerId, vehicleId, cancellationToken);
        var now = time.GetUtcNow().UtcDateTime;

        var blocks = await db.VehicleCalendarBlocks.AsNoTracking()
            .Where(b => b.PartnerVehicleId == vehicleId && b.EndAt > now)
            .Where(b => b.Status == Consumed || (b.Status == Active && (b.ExpiresAt == null || b.ExpiresAt > now)))
            .OrderBy(b => b.StartAt)
            .Select(b => new
            {
                b.CalendarBlockId,
                b.PartnerVehicleId,
                b.BlockType,
                b.StartAt,
                b.EndAt,
                b.Notes,
                BookingNumber = b.SourceBooking == null ? null : b.SourceBooking.BookingNumber,
                b.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return blocks.Select(b => new CalendarBlockDto(
            b.CalendarBlockId, b.PartnerVehicleId, EnumValue.Parse<CalendarBlockType>(b.BlockType),
            b.StartAt, b.EndAt, b.Notes, b.BookingNumber, b.CreatedAt)).ToList();
    }

    public async Task<CalendarBlockDto> AddBlockAsync(int rentalPartnerId, int vehicleId, AddCalendarBlockCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        if (command.BlockType == CalendarBlockType.Reservation)
        {
            throw new BusinessRuleException("Reservations are created by bookings, not by hand.");
        }

        var start = GoaClock.TruncateToMinute(command.StartAt.UtcDateTime);
        var end = GoaClock.TruncateToMinute(command.EndAt.UtcDateTime);
        var now = time.GetUtcNow().UtcDateTime;
        if (end <= start) throw new BusinessRuleException("The end must be after the start.");
        if (end <= now) throw new BusinessRuleException("The block must end in the future.");
        if (end - start > TimeSpan.FromDays(366)) throw new BusinessRuleException("Blocks can be at most a year long. Retire the vehicle instead.");

        await EnsureOwnedAsync(rentalPartnerId, vehicleId, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await VehicleLock.AcquireAsync(db, vehicleId, cancellationToken);

        // Keep the turnaround gap free around customer reservations too.
        var buffer = TimeSpan.FromMinutes(allocation.Value.TurnaroundBufferMinutes);
        var clash = await db.VehicleCalendarBlocks
            .Where(b => b.PartnerVehicleId == vehicleId && b.BlockType == Reservation)
            .Where(b => b.Status == Consumed || (b.Status == Active && (b.ExpiresAt == null || b.ExpiresAt > now)))
            .AnyAsync(b => b.StartAt < end + buffer && b.EndAt > start - buffer, cancellationToken);
        if (clash)
        {
            throw new ConflictException("The vehicle has a customer booking in this period. Choose different times.");
        }

        var block = new VehicleCalendarBlock
        {
            PartnerVehicleId = vehicleId,
            StartAt = start,
            EndAt = end,
            BlockType = EnumValue.ToByte(command.BlockType),
            Status = Active,
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            CreatedAt = now,
            CreatedBy = actorUserId,
        };
        db.VehicleCalendarBlocks.Add(block);
        await db.SaveChangesAsync(cancellationToken);
        AuditWriter.Add(db, actorUserId, "CalendarBlockAdded", nameof(VehicleCalendarBlock), block.CalendarBlockId,
            new { vehicleId, Type = command.BlockType.ToString(), start, end });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CalendarBlockDto(block.CalendarBlockId, vehicleId, command.BlockType, start, end, block.Notes, null, block.CreatedAt);
    }

    public async Task RemoveBlockAsync(int rentalPartnerId, int calendarBlockId, int actorUserId, CancellationToken cancellationToken = default)
    {
        var block = await db.VehicleCalendarBlocks
            .SingleOrDefaultAsync(b => b.CalendarBlockId == calendarBlockId && b.PartnerVehicle.RentalPartnerId == rentalPartnerId, cancellationToken)
            ?? throw new NotFoundException("Calendar block");

        if (block.BlockType == Reservation)
        {
            throw new BusinessRuleException("Customer bookings can't be removed from the calendar.");
        }

        if (block.Status != Active)
        {
            return;
        }

        block.Status = EnumValue.ToByte(ReservationStatus.Released);
        AuditWriter.Add(db, actorUserId, "CalendarBlockRemoved", nameof(VehicleCalendarBlock), calendarBlockId);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureOwnedAsync(int rentalPartnerId, int vehicleId, CancellationToken cancellationToken)
    {
        if (!await db.PartnerVehicles.AnyAsync(v => v.PartnerVehicleId == vehicleId && v.RentalPartnerId == rentalPartnerId, cancellationToken))
        {
            throw new NotFoundException("Vehicle");
        }
    }
}
