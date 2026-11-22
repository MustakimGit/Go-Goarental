using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Availability;
using GoGoaRentals.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace GoGoaRentals.Services.Dashboard;

/// <summary>Whether a vehicle can be given to a customer right now, and if not, the first reason why. Computed, not stored.</summary>
public enum VehicleAvailability
{
    AvailableNow,
    BusyNow,
    SwitchedOff,
    AwaitingChecks,
    PartnerNotActive,
    TypeDisabled,
    Retired,
}

public sealed record DashboardTotals(
    int Customers,
    int Partners,
    int PartnersAwaitingApproval,
    int Vehicles,
    int VehiclesAvailableNow);

public sealed record DashboardVehicle(
    int PartnerVehicleId,
    string RegistrationNumber,
    string Make,
    string Model,
    string VehicleTypeName,
    string CategoryName,
    string PickupLocationName,
    VehicleStatus Status,
    DocumentVerificationStatus DocumentVerificationStatus,
    VehicleAvailability Availability);

public sealed record DashboardPartner(
    int RentalPartnerId,
    int UserId,
    string OwnerName,
    string Email,
    string ContactPhone,
    string BusinessName,
    PartnerStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<DashboardVehicle> Vehicles);

public sealed record AdminDashboard(DateTime GeneratedAtUtc, DashboardTotals Totals, IReadOnlyList<DashboardPartner> Partners);

public interface IAdminDashboardService
{
    /// <summary>Every partner account with its vehicles and whether each one is bookable at this moment.</summary>
    Task<AdminDashboard> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminDashboardService(AppDbContext db, TimeProvider time) : IAdminDashboardService
{
    public async Task<AdminDashboard> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var customerRole = EnumValue.ToByte(UserRole.Customer);
        var deleted = EnumValue.ToByte(UserStatus.Deleted);

        var partners = await db.RentalPartners.AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.PartnerVehicles).ThenInclude(v => v.VehicleType).ThenInclude(t => t.VehicleCategory)
            .Include(p => p.PartnerVehicles).ThenInclude(v => v.PickupLocation)
            .Where(p => p.User.Status != deleted)
            .OrderBy(p => p.PartnerStatus).ThenBy(p => p.BusinessName)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // Same calendar rule as search, applied to this minute with no turnaround buffer.
        var freeNow = (await db.PartnerVehicles.AsNoTracking()
                .Where(AvailabilityService.FreeBetween(now, now.AddMinutes(1), 0, now))
                .Select(v => v.PartnerVehicleId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var result = partners.Select(p => new DashboardPartner(
                p.RentalPartnerId,
                p.UserId,
                p.User.Name,
                p.User.Email,
                p.ContactPhone,
                p.BusinessName,
                EnumValue.Parse<PartnerStatus>(p.PartnerStatus),
                p.CreatedAt,
                p.PartnerVehicles
                    .OrderBy(v => v.RegistrationNumber)
                    .Select(v => new DashboardVehicle(
                        v.PartnerVehicleId,
                        v.RegistrationNumber,
                        v.Make,
                        v.Model,
                        v.VehicleType.Name,
                        v.VehicleType.VehicleCategory.Name,
                        v.PickupLocation.Name,
                        EnumValue.Parse<VehicleStatus>(v.Status),
                        EnumValue.Parse<DocumentVerificationStatus>(v.DocumentVerificationStatus),
                        AvailabilityOf(p, v, freeNow.Contains(v.PartnerVehicleId))))
                    .ToList()))
            .ToList();

        var vehicles = result.SelectMany(p => p.Vehicles).ToList();
        var totals = new DashboardTotals(
            await db.Users.CountAsync(u => u.Role == customerRole && u.Status != deleted, cancellationToken),
            result.Count,
            result.Count(p => p.Status == PartnerStatus.PendingApproval),
            vehicles.Count(v => v.Status != VehicleStatus.Retired),
            vehicles.Count(v => v.Availability == VehicleAvailability.AvailableNow));

        return new AdminDashboard(now, totals, result);
    }

    /// <summary>Mirrors AvailabilityService.BookableVehicles, reporting the first rule that fails.</summary>
    public static VehicleAvailability AvailabilityOf(RentalPartner partner, PartnerVehicle vehicle, bool freeNow)
    {
        var status = EnumValue.Parse<VehicleStatus>(vehicle.Status);
        if (status == VehicleStatus.Retired) return VehicleAvailability.Retired;
        if (partner.PartnerStatus != EnumValue.ToByte(PartnerStatus.Active)) return VehicleAvailability.PartnerNotActive;
        if (status == VehicleStatus.Inactive) return VehicleAvailability.SwitchedOff;
        if (status != VehicleStatus.Active || vehicle.DocumentVerificationStatus != EnumValue.ToByte(DocumentVerificationStatus.Verified))
        {
            return VehicleAvailability.AwaitingChecks;
        }

        if (vehicle.VehicleType.Status != EnumValue.ToByte(VehicleTypeStatus.Active) || !vehicle.VehicleType.VehicleCategory.IsActive)
        {
            return VehicleAvailability.TypeDisabled;
        }

        return freeNow ? VehicleAvailability.AvailableNow : VehicleAvailability.BusyNow;
    }
}
