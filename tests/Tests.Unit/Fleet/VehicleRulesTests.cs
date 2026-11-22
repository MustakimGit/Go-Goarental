using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Common;
using GoGoaRentals.Services.Fleet;

namespace GoGoaRentals.Tests.Unit.Fleet;

public class VehicleRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    [Theory]
    [InlineData("GA07AB1234", "GA07AB1234")]
    [InlineData("ga 07 ab-1234", "GA07AB1234")]
    [InlineData("GA-01-Z-12", "GA01Z12")]
    [InlineData("22 BH 1234 AA", "22BH1234AA")]
    public void Registration_numbers_are_normalized(string input, string expected)
        => Assert.Equal(expected, PartnerFleetService.NormalizeRegistration(input));

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("GOA07AB1234")]
    [InlineData("GA07AB12345")]
    public void Invalid_registration_numbers_are_rejected(string input)
        => Assert.Throws<BusinessRuleException>(() => PartnerFleetService.NormalizeRegistration(input));

    [Fact]
    public void All_required_documents_verified_and_unexpired_means_verified()
    {
        var docs = VehicleVerification.RequiredDocuments.Select(t => Doc(t, DocumentVerificationStatus.Verified)).ToList();

        Assert.Equal(DocumentVerificationStatus.Verified, VehicleVerification.Evaluate(docs, Today));
    }

    [Fact]
    public void A_missing_document_keeps_the_vehicle_pending()
    {
        var docs = VehicleVerification.RequiredDocuments.Skip(1).Select(t => Doc(t, DocumentVerificationStatus.Verified)).ToList();

        Assert.Equal(DocumentVerificationStatus.Pending, VehicleVerification.Evaluate(docs, Today));
    }

    [Fact]
    public void Rejected_latest_upload_without_a_valid_copy_means_rejected()
    {
        var docs = VehicleVerification.RequiredDocuments
            .Select(t => Doc(t, t == VehicleDocumentType.Insurance ? DocumentVerificationStatus.Rejected : DocumentVerificationStatus.Verified))
            .ToList();

        Assert.Equal(DocumentVerificationStatus.Rejected, VehicleVerification.Evaluate(docs, Today));
    }

    [Fact]
    public void An_expired_verified_document_means_expired()
    {
        var docs = VehicleVerification.RequiredDocuments
            .Select(t => Doc(t, DocumentVerificationStatus.Verified, t == VehicleDocumentType.PollutionCertificate ? Today.AddDays(-1) : Today.AddYears(1)))
            .ToList();

        Assert.Equal(DocumentVerificationStatus.Expired, VehicleVerification.Evaluate(docs, Today));
    }

    [Fact]
    public void A_pending_renewal_does_not_take_a_verified_vehicle_offline()
    {
        var docs = VehicleVerification.RequiredDocuments.Select(t => Doc(t, DocumentVerificationStatus.Verified)).ToList();
        docs.Add(Doc(VehicleDocumentType.Insurance, DocumentVerificationStatus.Pending, createdAt: DateTime.UtcNow.AddMinutes(1)));

        Assert.Equal(DocumentVerificationStatus.Verified, VehicleVerification.Evaluate(docs, Today));
    }

    [Fact]
    public void Refresh_moves_vehicles_in_and_out_of_service_but_keeps_partner_choices()
    {
        var verified = VehicleVerification.RequiredDocuments.Select(t => Doc(t, DocumentVerificationStatus.Verified)).ToList();

        var pending = Vehicle(VehicleStatus.PendingVerification, verified);
        VehicleVerification.Refresh(pending, Today);
        Assert.Equal(EnumValue.ToByte(VehicleStatus.Active), pending.Status);

        var paused = Vehicle(VehicleStatus.Inactive, verified);
        VehicleVerification.Refresh(paused, Today);
        Assert.Equal(EnumValue.ToByte(VehicleStatus.Inactive), paused.Status);

        var live = Vehicle(VehicleStatus.Active, verified.Skip(1).ToList());
        VehicleVerification.Refresh(live, Today);
        Assert.Equal(EnumValue.ToByte(VehicleStatus.PendingVerification), live.Status);
    }

    private static VehicleDocument Doc(VehicleDocumentType type, DocumentVerificationStatus status, DateOnly? expires = null, DateTime? createdAt = null) => new()
    {
        DocumentType = EnumValue.ToByte(type),
        VerificationStatus = EnumValue.ToByte(status),
        ExpiresOn = type == VehicleDocumentType.RegistrationCertificate ? null : expires ?? Today.AddYears(1),
        CreatedAt = createdAt ?? DateTime.UtcNow,
        FileUrl = "test",
    };

    private static PartnerVehicle Vehicle(VehicleStatus status, List<VehicleDocument> documents) => new()
    {
        Status = EnumValue.ToByte(status),
        VehicleDocuments = documents,
    };
}
