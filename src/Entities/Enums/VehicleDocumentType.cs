namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in VehicleDocument.DocumentType. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum VehicleDocumentType : byte
{
    RegistrationCertificate = 1,
    Insurance = 2,
    PollutionCertificate = 3,
    RentalPermit = 4,
}
