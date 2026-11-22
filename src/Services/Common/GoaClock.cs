namespace GoGoaRentals.Services.Common;

/// <summary>Instants are stored in UTC; calendar dates (document expiry, rental days) are in Indian Standard Time.</summary>
public static class GoaClock
{
    public static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public static DateOnly Today(TimeProvider time)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), TimeZone).DateTime);

    /// <summary>Drops seconds and below; instants are stored as datetime2(0) and compared to the minute.</summary>
    public static DateTime TruncateToMinute(DateTime utc)
        => new(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
}
