using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Common;
using GoGoaRentals.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace GoGoaRentals.Services.Fleet;

/// <summary>Admin checks of partner vehicles and their documents.</summary>
public interface IVehicleReviewService
{
    /// <summary>With needsReview, only vehicles that have at least one document waiting for a decision.</summary>
    Task<IReadOnlyList<PartnerVehicleDto>> ListAsync(bool needsReview, CancellationToken cancellationToken = default);

    Task<PartnerVehicleDto> ReviewDocumentAsync(int documentId, bool approve, string? reason, int actorUserId, CancellationToken cancellationToken = default);

    Task<DocumentFile> OpenDocumentAsync(int documentId, CancellationToken cancellationToken = default);
}

public sealed class VehicleReviewService(AppDbContext db, IFileStorage storage, TimeProvider time) : IVehicleReviewService
{
    private static readonly byte PendingDocument = EnumValue.ToByte(DocumentVerificationStatus.Pending);

    public async Task<IReadOnlyList<PartnerVehicleDto>> ListAsync(bool needsReview, CancellationToken cancellationToken = default)
    {
        var vehicles = await FleetQueries.WithDetails(db)
            .Where(v => !needsReview || v.VehicleDocuments.Any(d => d.VerificationStatus == PendingDocument))
            .OrderBy(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
        return vehicles.Select(FleetQueries.ToDto).ToList();
    }

    public async Task<PartnerVehicleDto> ReviewDocumentAsync(int documentId, bool approve, string? reason, int actorUserId, CancellationToken cancellationToken = default)
    {
        var document = await db.VehicleDocuments
            .Include(d => d.PartnerVehicle).ThenInclude(v => v.VehicleDocuments)
            .SingleOrDefaultAsync(d => d.VehicleDocumentId == documentId, cancellationToken)
            ?? throw new NotFoundException("Document");

        if (document.VerificationStatus != PendingDocument)
        {
            throw new BusinessRuleException("This document has already been reviewed.");
        }

        var today = GoaClock.Today(time);
        if (approve && document.ExpiresOn is { } expires && expires < today)
        {
            throw new BusinessRuleException("This document has expired and can't be approved.");
        }

        if (!approve && string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("Give a reason so the partner knows what to fix.");
        }

        var now = DateTime.UtcNow;
        document.VerificationStatus = EnumValue.ToByte(approve ? DocumentVerificationStatus.Verified : DocumentVerificationStatus.Rejected);
        document.VerifiedByUserId = actorUserId;
        document.VerifiedAt = now;
        document.ReviewNote = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        var vehicle = document.PartnerVehicle;
        VehicleVerification.Refresh(vehicle, today);
        vehicle.UpdatedAt = now;

        AuditWriter.Add(db, actorUserId, approve ? "VehicleDocumentVerified" : "VehicleDocumentRejected", nameof(VehicleDocument), documentId,
            new { vehicle.PartnerVehicleId, Reason = reason });
        await db.SaveChangesAsync(cancellationToken);

        var updated = await FleetQueries.WithDetails(db).SingleAsync(v => v.PartnerVehicleId == vehicle.PartnerVehicleId, cancellationToken);
        return FleetQueries.ToDto(updated);
    }

    public async Task<DocumentFile> OpenDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await db.VehicleDocuments.AsNoTracking()
            .Include(d => d.PartnerVehicle)
            .SingleOrDefaultAsync(d => d.VehicleDocumentId == documentId, cancellationToken)
            ?? throw new NotFoundException("Document");
        return FleetQueries.OpenDocument(storage, document, document.PartnerVehicle.RegistrationNumber);
    }
}
