using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Dashboard;

namespace GoGoaRentals.Tests.Unit.Dashboard;

public class VehicleAvailabilityTests
{
    [Fact]
    public void Live_verified_free_vehicle_of_active_partner_is_available_now()
        => Assert.Equal(VehicleAvailability.AvailableNow, Evaluate());

    [Fact]
    public void A_calendar_block_right_now_makes_it_busy()
        => Assert.Equal(VehicleAvailability.BusyNow, Evaluate(freeNow: false));

    [Fact]
    public void Partner_switched_it_off()
        => Assert.Equal(VehicleAvailability.SwitchedOff, Evaluate(status: VehicleStatus.Inactive));

    [Theory]
    [InlineData(VehicleStatus.PendingVerification, DocumentVerificationStatus.Pending)]
    [InlineData(VehicleStatus.Active, DocumentVerificationStatus.Expired)]
    public void Unverified_paperwork_means_awaiting_checks(VehicleStatus status, DocumentVerificationStatus documents)
        => Assert.Equal(VehicleAvailability.AwaitingChecks, Evaluate(status: status, documents: documents));

    [Fact]
    public void Suspended_partner_hides_all_their_vehicles()
        => Assert.Equal(VehicleAvailability.PartnerNotActive, Evaluate(partnerStatus: PartnerStatus.Suspended));

    [Fact]
    public void Disabled_vehicle_type_is_not_bookable()
        => Assert.Equal(VehicleAvailability.TypeDisabled, Evaluate(typeStatus: VehicleTypeStatus.Inactive));

    [Fact]
    public void Retired_wins_over_everything_else()
        => Assert.Equal(VehicleAvailability.Retired, Evaluate(status: VehicleStatus.Retired, partnerStatus: PartnerStatus.Suspended));

    private static VehicleAvailability Evaluate(
        VehicleStatus status = VehicleStatus.Active,
        DocumentVerificationStatus documents = DocumentVerificationStatus.Verified,
        PartnerStatus partnerStatus = PartnerStatus.Active,
        VehicleTypeStatus typeStatus = VehicleTypeStatus.Active,
        bool freeNow = true)
    {
        var partner = new RentalPartner { PartnerStatus = EnumValue.ToByte(partnerStatus) };
        var vehicle = new PartnerVehicle
        {
            Status = EnumValue.ToByte(status),
            DocumentVerificationStatus = EnumValue.ToByte(documents),
            VehicleType = new VehicleType
            {
                Status = EnumValue.ToByte(typeStatus),
                VehicleCategory = new VehicleCategory { IsActive = true },
            },
        };
        return AdminDashboardService.AvailabilityOf(partner, vehicle, freeNow);
    }
}
