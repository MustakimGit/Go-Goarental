using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace GoGoaRentals.Services.Partners;

public sealed record PartnerSummary(
    int RentalPartnerId,
    string BusinessName,
    string OwnerName,
    string Email,
    string ContactPhone,
    string? Address,
    PartnerStatus Status,
    decimal ReliabilityScore,
    decimal CommissionPercent,
    int VehicleCount,
    DateTime CreatedAt);

public interface IPartnerAdminService
{
    Task<IReadOnlyList<PartnerSummary>> ListAsync(PartnerStatus? status, CancellationToken cancellationToken = default);

    Task<PartnerSummary> ChangeStatusAsync(int rentalPartnerId, PartnerStatus newStatus, string? reason, int actorUserId, CancellationToken cancellationToken = default);

    Task<PartnerSummary> SetCommissionAsync(int rentalPartnerId, decimal commissionPercent, int actorUserId, CancellationToken cancellationToken = default);
}

public sealed class PartnerAdminService(AppDbContext db) : IPartnerAdminService
{
    // Which status changes an admin may make. Rejected partners can be reconsidered.
    private static readonly Dictionary<PartnerStatus, PartnerStatus[]> AllowedTransitions = new()
    {
        [PartnerStatus.PendingApproval] = [PartnerStatus.Active, PartnerStatus.Rejected],
        [PartnerStatus.Active] = [PartnerStatus.Suspended],
        [PartnerStatus.Suspended] = [PartnerStatus.Active],
        [PartnerStatus.Rejected] = [PartnerStatus.Active],
    };

    public async Task<IReadOnlyList<PartnerSummary>> ListAsync(PartnerStatus? status, CancellationToken cancellationToken = default)
    {
        var query = db.RentalPartners.AsNoTracking();
        if (status is { } s)
        {
            var value = EnumValue.ToByte(s);
            query = query.Where(p => p.PartnerStatus == value);
        }

        return await ProjectAsync(query.OrderBy(p => p.PartnerStatus).ThenByDescending(p => p.CreatedAt), cancellationToken);
    }

    public async Task<PartnerSummary> ChangeStatusAsync(int rentalPartnerId, PartnerStatus newStatus, string? reason, int actorUserId, CancellationToken cancellationToken = default)
    {
        var partner = await db.RentalPartners.SingleOrDefaultAsync(p => p.RentalPartnerId == rentalPartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner");

        var current = EnumValue.Parse<PartnerStatus>(partner.PartnerStatus);
        if (!AllowedTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(newStatus))
        {
            throw new BusinessRuleException($"A partner that is {current} can't be changed to {newStatus}.");
        }

        partner.PartnerStatus = EnumValue.ToByte(newStatus);
        partner.UpdatedAt = DateTime.UtcNow;
        AuditWriter.Add(db, actorUserId, "PartnerStatusChanged", "RentalPartner", rentalPartnerId,
            new { From = current.ToString(), To = newStatus.ToString(), Reason = reason });
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(rentalPartnerId, cancellationToken);
    }

    public async Task<PartnerSummary> SetCommissionAsync(int rentalPartnerId, decimal commissionPercent, int actorUserId, CancellationToken cancellationToken = default)
    {
        if (commissionPercent is < 0 or > 100)
        {
            throw new BusinessRuleException("Commission must be between 0 and 100 percent.");
        }

        var partner = await db.RentalPartners.SingleOrDefaultAsync(p => p.RentalPartnerId == rentalPartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner");

        AuditWriter.Add(db, actorUserId, "PartnerCommissionChanged", "RentalPartner", rentalPartnerId,
            new { From = partner.CommissionPercent, To = commissionPercent });
        partner.CommissionPercent = commissionPercent;
        partner.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(rentalPartnerId, cancellationToken);
    }

    private async Task<PartnerSummary> GetAsync(int rentalPartnerId, CancellationToken cancellationToken)
        => (await ProjectAsync(db.RentalPartners.AsNoTracking().Where(p => p.RentalPartnerId == rentalPartnerId), cancellationToken)).Single();

    private static async Task<IReadOnlyList<PartnerSummary>> ProjectAsync(IQueryable<RentalPartner> query, CancellationToken cancellationToken)
    {
        var rows = await query
            .Select(p => new
            {
                p.RentalPartnerId,
                p.BusinessName,
                OwnerName = p.User.Name,
                p.User.Email,
                p.ContactPhone,
                p.Address,
                p.PartnerStatus,
                p.ReliabilityScore,
                p.CommissionPercent,
                VehicleCount = p.PartnerVehicles.Count,
                p.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new PartnerSummary(
            r.RentalPartnerId, r.BusinessName, r.OwnerName, r.Email, r.ContactPhone, r.Address,
            EnumValue.Parse<PartnerStatus>(r.PartnerStatus), r.ReliabilityScore, r.CommissionPercent, r.VehicleCount, r.CreatedAt))
            .ToList();
    }
}
