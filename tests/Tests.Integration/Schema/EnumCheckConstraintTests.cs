using System.Text.RegularExpressions;
using GoGoaRentals.Entities.Enums;
using Microsoft.Data.SqlClient;

namespace GoGoaRentals.Tests.Integration.Schema;

/// <summary>
/// Guards the database-first workflow: every C# enum must allow exactly the values the SQL CHECK constraint allows.
/// Fails when a value is added in SSMS but not in code (or vice versa).
/// </summary>
public partial class EnumCheckConstraintTests
{
    public static TheoryData<string, string, Type> Constraints => new()
    {
        { "CK_Users_Role", "Role", typeof(UserRole) },
        { "CK_Users_Status", "Status", typeof(UserStatus) },
        { "CK_UserNotificationPreference_Channel", "Channel", typeof(NotificationChannel) },
        { "CK_Notification_Channel", "Channel", typeof(NotificationChannel) },
        { "CK_NotificationTemplate_Channel", "Channel", typeof(NotificationChannel) },
        { "CK_RentalPartner_Status", "PartnerStatus", typeof(PartnerStatus) },
        { "CK_VehicleType_Status", "Status", typeof(VehicleTypeStatus) },
        { "CK_PartnerVehicle_Status", "Status", typeof(VehicleStatus) },
        { "CK_PartnerVehicle_DocStatus", "DocumentVerificationStatus", typeof(DocumentVerificationStatus) },
        { "CK_VehicleDocument_Status", "VerificationStatus", typeof(DocumentVerificationStatus) },
        { "CK_VehicleDocument_Type", "DocumentType", typeof(VehicleDocumentType) },
        { "CK_VehiclePricing_DurationType", "DurationType", typeof(DurationType) },
        { "CK_Booking_PickupMode", "PickupMode", typeof(PickupMode) },
        { "CK_Booking_Status", "BookingStatus", typeof(BookingStatus) },
        { "CK_Booking_CancelReason", "CancellationReason", typeof(CancellationReason) },
        { "CK_BookingAssignment_Status", "AssignmentStatus", typeof(BookingAssignmentStatus) },
        { "CK_VehicleCalendarBlock_Type", "BlockType", typeof(CalendarBlockType) },
        { "CK_VehicleCalendarBlock_Status", "Status", typeof(ReservationStatus) },
        { "CK_Payment_Gateway", "Gateway", typeof(PaymentGateway) },
        { "CK_Payment_Status", "PaymentStatus", typeof(PaymentStatus) },
        { "CK_PaymentWebhookEvent_Status", "ProcessingStatus", typeof(WebhookProcessingStatus) },
        { "CK_Refund_Status", "RefundStatus", typeof(RefundStatus) },
        { "CK_PartnerSettlement_Status", "SettlementStatus", typeof(PartnerSettlementStatus) },
        { "CK_NotificationOutbox_Status", "ProcessingStatus", typeof(OutboxStatus) },
        { "CK_Notification_Status", "NotificationStatus", typeof(NotificationStatus) },
        { "CK_NotificationAttempt_Result", "Result", typeof(NotificationAttemptResult) },
    };

    [Theory]
    [MemberData(nameof(Constraints))]
    public async Task Enum_values_match_check_constraint(string constraintName, string column, Type enumType)
    {
        await using var connection = new SqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT definition FROM sys.check_constraints WHERE name = @name", connection);
        command.Parameters.AddWithValue("@name", constraintName);
        var definition = (string?)await command.ExecuteScalarAsync();
        Assert.False(definition is null, $"Check constraint {constraintName} not found.");

        // SQL Server normalizes "Col IN (1,2)" to "[Col]=(1) OR [Col]=(2)".
        var allowedInDb = Regex.Matches(definition!, $@"\[{Regex.Escape(column)}\]=\((\d+)\)")
            .Select(m => int.Parse(m.Groups[1].Value))
            .Order()
            .ToArray();

        var definedInCode = Enum.GetValues(enumType).Cast<object>()
            .Select(Convert.ToInt32)
            .Order()
            .ToArray();

        Assert.Equal(definedInCode, allowedInDb);
    }
}
