using GoGoaRentals.Services.Common;

namespace GoGoaRentals.Services.Pricing;

/// <summary>
/// Rental days run from <c>startHour</c> Goa time to the same hour the next day (9 am to 9 am by default).
/// A rental is charged for every rental day it touches, e.g. with 9 am days:
/// 9 Nov 09:00 → 10 Nov 09:00 is 1 day; 9 Nov 14:00 → 10 Nov 11:00 is 2 days.
/// A rental day is named by the date it starts on.
/// </summary>
public static class RentalDays
{
    public static IReadOnlyList<DateOnly> Between(DateTime pickupUtc, DateTime returnUtc, int startHour)
    {
        if (returnUtc <= pickupUtc)
        {
            throw new ArgumentException("Return must be after pickup.");
        }

        var offset = TimeSpan.FromHours(startHour);
        var first = DateOnly.FromDateTime(ToGoaTime(pickupUtc) - offset);
        // The return instant is exclusive, so returning exactly at the day boundary doesn't start a new day.
        var last = DateOnly.FromDateTime(ToGoaTime(returnUtc) - offset - TimeSpan.FromTicks(1));

        var days = new List<DateOnly>();
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            days.Add(day);
        }

        return days;
    }

    /// <summary>The UTC instant a rental day begins.</summary>
    public static DateTime DayStartUtc(DateOnly day, int startHour)
        => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(startHour, 0)), GoaClock.TimeZone);

    /// <summary>The rental day an instant falls in.</summary>
    public static DateOnly DayOf(DateTime utc, int startHour)
        => DateOnly.FromDateTime(ToGoaTime(utc) - TimeSpan.FromHours(startHour));

    private static DateTime ToGoaTime(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), GoaClock.TimeZone);
}
