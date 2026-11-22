using GoGoaRentals.Services.Pricing;

namespace GoGoaRentals.Tests.Unit.Pricing;

public class RentalDaysTests
{
    private const int NineAm = 9;

    // Goa is UTC+05:30, so 09:00 IST = 03:30 UTC.
    private static DateTime Ist(int day, int hour, int minute = 0)
        => new DateTime(2026, 11, day, hour, minute, 0, DateTimeKind.Utc).AddHours(-5.5);

    [Theory]
    [InlineData(9, 9, 10, 9, 1)]   // exactly one 9-to-9 day
    [InlineData(9, 14, 10, 9, 1)]  // afternoon pickup, back by 9 am
    [InlineData(9, 14, 10, 11, 2)] // back after 9 am starts a second day
    [InlineData(9, 8, 9, 18, 2)]   // 8 am belongs to the previous rental day
    [InlineData(9, 9, 9, 10, 1)]   // one hour still costs a day
    [InlineData(9, 9, 16, 9, 7)]   // a week
    public void Counts_every_rental_day_touched(int pickupDay, int pickupHour, int returnDay, int returnHour, int expected)
        => Assert.Equal(expected, RentalDays.Between(Ist(pickupDay, pickupHour), Ist(returnDay, returnHour), NineAm).Count);

    [Fact]
    public void Days_are_named_by_the_date_they_start()
    {
        var days = RentalDays.Between(Ist(9, 8), Ist(10, 10), NineAm);

        Assert.Equal([new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 10)], days);
    }

    [Fact]
    public void Day_start_is_9am_goa_time_in_utc()
        => Assert.Equal(new DateTime(2026, 11, 9, 3, 30, 0, DateTimeKind.Utc), RentalDays.DayStartUtc(new DateOnly(2026, 11, 9), NineAm));

    [Fact]
    public void Return_before_pickup_throws()
        => Assert.Throws<ArgumentException>(() => RentalDays.Between(Ist(10, 9), Ist(9, 9), NineAm));
}
