using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GoGoaRentals.Tests.Integration.Api;

/// <summary>
/// Each test makes its own vehicle type and pickup location so prices and availability counts are isolated.
/// Rental days run 9 am to 9 am Goa time (+05:30).
/// </summary>
public class PricingAndSearchTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

    /// <summary>"yyyy-MM-ddTHH:mm:00+05:30" for a Goa date and time.</summary>
    private static string Goa(DateOnly day, int hour, int minute = 0) => $"{day:yyyy-MM-dd}T{hour:00}:{minute:00}:00+05:30";

    [Fact]
    public async Task Days_are_counted_9am_to_9am_and_totals_add_up()
    {
        using var admin = await factory.AdminClientAsync();
        var (typeId, locationId) = await CreateTypeAndLocationAsync(admin, deliveryFee: 250);
        await AddPriceAsync(admin, typeId, 500, deposit: 1000, tax: 5, firstDay: Today);
        var day = Today.AddDays(7);

        async Task<JsonElement> QuoteAsync(string pickup, string ret, string mode = "SelfPickup")
        {
            var response = await admin.PostAsJsonAsync("/api/quotes", new { vehicleTypeId = typeId, pickupLocationId = locationId, pickupMode = mode, pickupAt = pickup, returnAt = ret });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        Assert.Equal(1, (await QuoteAsync(Goa(day, 9), Goa(day.AddDays(1), 9))).GetProperty("rentalDays").GetInt32());
        Assert.Equal(1, (await QuoteAsync(Goa(day, 14), Goa(day.AddDays(1), 9))).GetProperty("rentalDays").GetInt32());
        Assert.Equal(2, (await QuoteAsync(Goa(day, 14), Goa(day.AddDays(1), 11))).GetProperty("rentalDays").GetInt32());
        Assert.Equal(2, (await QuoteAsync(Goa(day, 8), Goa(day, 18))).GetProperty("rentalDays").GetInt32());

        var quote = await QuoteAsync(Goa(day, 9), Goa(day.AddDays(3), 9), mode: "Delivery");
        Assert.Equal(3, quote.GetProperty("rentalDays").GetInt32());
        Assert.Equal(1500m, quote.GetProperty("rentalPrice").GetDecimal());
        Assert.Equal(75m, quote.GetProperty("taxAmount").GetDecimal());
        Assert.Equal(250m, quote.GetProperty("deliveryFee").GetDecimal());
        Assert.Equal(1000m, quote.GetProperty("depositAmount").GetDecimal());
        Assert.Equal(2825m, quote.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task Peak_rate_applies_only_to_its_days_and_the_base_rate_resumes_after()
    {
        using var admin = await factory.AdminClientAsync();
        var (typeId, locationId) = await CreateTypeAndLocationAsync(admin);
        await AddPriceAsync(admin, typeId, 500, firstDay: Today);
        var peakStart = Today.AddDays(10);
        await AddPriceAsync(admin, typeId, 900, firstDay: peakStart, lastDay: peakStart.AddDays(1));

        var response = await admin.PostAsJsonAsync("/api/quotes", new
        {
            vehicleTypeId = typeId,
            pickupLocationId = locationId,
            pickupAt = Goa(peakStart.AddDays(-1), 9),
            returnAt = Goa(peakStart.AddDays(3), 9),
        });
        var quote = await response.Content.ReadFromJsonAsync<JsonElement>();

        var dayPrices = quote.GetProperty("days").EnumerateArray().Select(d => d.GetProperty("price").GetDecimal()).ToList();
        Assert.Equal([500m, 900m, 900m, 500m], dayPrices);
        Assert.Equal(2800m, quote.GetProperty("rentalPrice").GetDecimal());

        var prices = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/prices?vehicleTypeId={typeId}");
        Assert.Equal(3, prices.GetArrayLength()); // base (ended), peak, base resumed
    }

    [Fact]
    public async Task Overlapping_bounded_prices_are_refused()
    {
        using var admin = await factory.AdminClientAsync();
        var (typeId, _) = await CreateTypeAndLocationAsync(admin);
        await AddPriceAsync(admin, typeId, 500, firstDay: Today, lastDay: Today.AddDays(20));

        var overlap = await admin.PostAsJsonAsync("/api/admin/prices", new
        {
            vehicleTypeId = typeId, dailyPrice = 700, firstDay = Today.AddDays(-5).ToString("yyyy-MM-dd"), lastDay = Today.AddDays(5).ToString("yyyy-MM-dd"),
        });

        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
    }

    [Fact]
    public async Task Types_without_a_price_for_every_day_are_not_offered()
    {
        using var admin = await factory.AdminClientAsync();
        var (typeId, locationId) = await CreateTypeAndLocationAsync(admin);
        await AddPriceAsync(admin, typeId, 500, firstDay: Today, lastDay: Today.AddDays(8));
        var day = Today.AddDays(7);

        var covered = await SearchAsync(admin, locationId, Goa(day, 9), Goa(day.AddDays(1), 9));
        Assert.Contains(covered.EnumerateArray(), q => q.GetProperty("vehicleTypeId").GetInt32() == typeId);

        var notCovered = await SearchAsync(admin, locationId, Goa(day, 9), Goa(day.AddDays(3), 9));
        Assert.DoesNotContain(notCovered.EnumerateArray(), q => q.GetProperty("vehicleTypeId").GetInt32() == typeId);
    }

    [Fact]
    public async Task Availability_counts_live_vehicles_and_respects_partner_blocks_with_buffer()
    {
        using var admin = await factory.AdminClientAsync();
        var (typeId, locationId) = await CreateTypeAndLocationAsync(admin);
        await AddPriceAsync(admin, typeId, 500, firstDay: Today);
        var day = Today.AddDays(7);
        var (pickup, ret) = (Goa(day, 9), Goa(day.AddDays(2), 9));

        Assert.Equal(0, await AvailableAsync(admin, typeId, locationId, pickup, ret));

        var (partner, _) = await factory.PartnerClientAsync(approve: true);
        using var _ = partner;
        var vehicleId = await factory.CreateLiveVehicleAsync(partner, typeId, locationId);
        Assert.Equal(1, await AvailableAsync(admin, typeId, locationId, pickup, ret));

        // Ends 30 minutes before pickup: inside the 60-minute turnaround buffer, so the vehicle isn't free.
        var block = await partner.PostAsJsonAsync($"/api/partner/vehicles/{vehicleId}/calendar", new
        {
            blockType = "Maintenance", startAt = Goa(day.AddDays(-1), 9), endAt = Goa(day, 8, 30), notes = "Service",
        });
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);
        Assert.Equal(0, await AvailableAsync(admin, typeId, locationId, pickup, ret));

        var blockId = (await block.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("calendarBlockId").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await partner.DeleteAsync($"/api/partner/calendar/{blockId}")).StatusCode);
        Assert.Equal(1, await AvailableAsync(admin, typeId, locationId, pickup, ret));

        // Paused vehicles aren't offered.
        (await partner.PutAsJsonAsync($"/api/partner/vehicles/{vehicleId}/status", new { status = "Inactive" })).EnsureSuccessStatusCode();
        Assert.Equal(0, await AvailableAsync(admin, typeId, locationId, pickup, ret));
    }

    [Fact]
    public async Task Partners_cannot_block_or_see_another_partners_calendar()
    {
        using var admin = await factory.AdminClientAsync();
        var (typeId, locationId) = await CreateTypeAndLocationAsync(admin);
        var (owner, _) = await factory.PartnerClientAsync(approve: true);
        var (other, _) = await factory.PartnerClientAsync(approve: true);
        using var _1 = owner;
        using var _2 = other;
        var vehicleId = (await (await owner.PostAsJsonAsync("/api/partner/vehicles", new
        {
            vehicleId = await factory.CatalogModelIdAsync(typeId), registrationNumber = ApiFactory.UniqueRegistration(),
            manufacturingYear = 2024, pickupLocationId = locationId, deliveryRadiusKm = 0,
        })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("partnerVehicleId").GetInt32();

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/partner/vehicles/{vehicleId}/calendar")).StatusCode);
        var block = await other.PostAsJsonAsync($"/api/partner/vehicles/{vehicleId}/calendar", new
        {
            blockType = "PartnerUnavailable", startAt = Goa(Today.AddDays(3), 9), endAt = Goa(Today.AddDays(4), 9),
        });
        Assert.Equal(HttpStatusCode.NotFound, block.StatusCode);
    }

    [Fact]
    public async Task Invalid_periods_are_rejected()
    {
        using var client = factory.NewClient();
        var location = (await client.GetFromJsonAsync<JsonElement>("/api/catalog/pickup-locations"))[0].GetProperty("pickupLocationId").GetInt32();

        var past = await client.GetAsync($"/api/search?pickupLocationId={location}&pickupAt={Uri.EscapeDataString(Goa(Today.AddDays(-1), 9))}&returnAt={Uri.EscapeDataString(Goa(Today.AddDays(1), 9))}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, past.StatusCode);

        var backwards = await client.GetAsync($"/api/search?pickupLocationId={location}&pickupAt={Uri.EscapeDataString(Goa(Today.AddDays(5), 9))}&returnAt={Uri.EscapeDataString(Goa(Today.AddDays(4), 9))}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, backwards.StatusCode);

        var tooLong = await client.GetAsync($"/api/search?pickupLocationId={location}&pickupAt={Uri.EscapeDataString(Goa(Today.AddDays(2), 9))}&returnAt={Uri.EscapeDataString(Goa(Today.AddDays(40), 9))}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLong.StatusCode);
    }

    private static async Task<(int TypeId, int LocationId)> CreateTypeAndLocationAsync(HttpClient admin, decimal deliveryFee = 0)
    {
        var categoryId = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/vehicle-categories"))[0].GetProperty("vehicleCategoryId").GetInt32();
        var type = await admin.PostAsJsonAsync("/api/admin/vehicle-types", new { vehicleCategoryId = categoryId, name = $"Test type {Guid.NewGuid():N}" });
        type.EnsureSuccessStatusCode();
        var location = await admin.PostAsJsonAsync("/api/admin/pickup-locations", new
        {
            name = $"Test location {Guid.NewGuid():N}", address = "Test road, Goa", latitude = 15.5, longitude = 73.8, customerDeliveryFee = deliveryFee, isActive = true,
        });
        location.EnsureSuccessStatusCode();
        return (
            (await type.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("vehicleTypeId").GetInt32(),
            (await location.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("pickupLocationId").GetInt32());
    }

    private static async Task AddPriceAsync(HttpClient admin, int typeId, decimal price, DateOnly firstDay, DateOnly? lastDay = null, decimal deposit = 0, decimal tax = 0)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/prices", new
        {
            vehicleTypeId = typeId, dailyPrice = price, depositAmount = deposit, taxPercent = tax,
            firstDay = firstDay.ToString("yyyy-MM-dd"), lastDay = lastDay?.ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<JsonElement> SearchAsync(HttpClient client, int locationId, string pickup, string ret)
    {
        var response = await client.GetAsync($"/api/search?pickupLocationId={locationId}&pickupAt={Uri.EscapeDataString(pickup)}&returnAt={Uri.EscapeDataString(ret)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<int> AvailableAsync(HttpClient client, int typeId, int locationId, string pickup, string ret)
        => (await SearchAsync(client, locationId, pickup, ret)).EnumerateArray()
            .Single(q => q.GetProperty("vehicleTypeId").GetInt32() == typeId)
            .GetProperty("availableVehicles").GetInt32();
}
