namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in PaymentWebhookEvent.ProcessingStatus. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum WebhookProcessingStatus : byte
{
    Received = 1,
    Processed = 2,
    Failed = 3,
    Ignored = 4,
}
