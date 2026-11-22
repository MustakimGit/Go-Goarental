namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in Payment.Gateway, PaymentWebhookEvent.Gateway. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum PaymentGateway : byte
{
    Razorpay = 1,
}
