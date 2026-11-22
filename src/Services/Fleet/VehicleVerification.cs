using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;

namespace GoGoaRentals.Services.Fleet;

/// <summary>Rules that decide whether a vehicle's paperwork lets it take bookings.</summary>
public static class VehicleVerification
{
    /// <summary>A vehicle needs a valid, verified copy of each of these before it can go live.</summary>
    public static readonly VehicleDocumentType[] RequiredDocuments =
    [
        VehicleDocumentType.RegistrationCertificate,
        VehicleDocumentType.Insurance,
        VehicleDocumentType.PollutionCertificate,
        VehicleDocumentType.RentalPermit,
    ];

    /// <summary>The RC has no practical expiry for our purposes; every other document must state one.</summary>
    public static bool RequiresExpiryDate(VehicleDocumentType type) => type != VehicleDocumentType.RegistrationCertificate;

    /// <summary>
    /// Overall status from the documents on file. For each required type the vehicle is covered if any
    /// verified copy is unexpired; otherwise the newest upload decides (pending, rejected or expired).
    /// </summary>
    public static DocumentVerificationStatus Evaluate(IEnumerable<VehicleDocument> documents, DateOnly today)
    {
        var byType = documents.ToLookup(d => d.DocumentType);
        var states = RequiredDocuments.Select(type =>
        {
            var docs = byType[EnumValue.ToByte(type)].ToList();
            if (docs.Any(d => IsValid(d, today)))
            {
                return DocumentVerificationStatus.Verified;
            }

            var latest = docs.MaxBy(d => d.CreatedAt);
            if (latest is null)
            {
                return DocumentVerificationStatus.Pending;
            }

            var status = EnumValue.Parse<DocumentVerificationStatus>(latest.VerificationStatus);
            return status == DocumentVerificationStatus.Verified ? DocumentVerificationStatus.Expired : status;
        }).ToList();

        if (states.All(s => s == DocumentVerificationStatus.Verified)) return DocumentVerificationStatus.Verified;
        if (states.Contains(DocumentVerificationStatus.Rejected)) return DocumentVerificationStatus.Rejected;
        if (states.Contains(DocumentVerificationStatus.Expired)) return DocumentVerificationStatus.Expired;
        return DocumentVerificationStatus.Pending;
    }

    /// <summary>
    /// Re-evaluates the vehicle's documents and moves it in or out of service. Partner choices
    /// (Inactive, Retired) are kept; only PendingVerification ⇄ Active changes automatically.
    /// </summary>
    public static void Refresh(PartnerVehicle vehicle, DateOnly today)
    {
        var documentStatus = Evaluate(vehicle.VehicleDocuments, today);
        vehicle.DocumentVerificationStatus = EnumValue.ToByte(documentStatus);

        var status = EnumValue.Parse<VehicleStatus>(vehicle.Status);
        if (documentStatus == DocumentVerificationStatus.Verified && status == VehicleStatus.PendingVerification)
        {
            vehicle.Status = EnumValue.ToByte(VehicleStatus.Active);
        }
        else if (documentStatus != DocumentVerificationStatus.Verified && status == VehicleStatus.Active)
        {
            vehicle.Status = EnumValue.ToByte(VehicleStatus.PendingVerification);
        }
    }

    private static bool IsValid(VehicleDocument document, DateOnly today)
        => document.VerificationStatus == EnumValue.ToByte(DocumentVerificationStatus.Verified)
           && (document.ExpiresOn is null || document.ExpiresOn >= today);
}
