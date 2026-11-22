using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GoGoaRentals.Tests.Integration.Api;

public class AdminDashboardTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Dashboard_shows_partner_vehicles_and_follows_the_partner_availability_switch()
    {
        using var admin = await factory.AdminClientAsync();
        var (partner, partnerId) = await factory.PartnerClientAsync(approve: true);
        using var _ = partner;

        var typeId = (await admin.GetFromJsonAsync<JsonElement>("/api/catalog/vehicle-types"))[0].GetProperty("vehicleTypeId").GetInt32();
        var locationId = (await admin.GetFromJsonAsync<JsonElement>("/api/catalog/pickup-locations"))[0].GetProperty("pickupLocationId").GetInt32();
        var vehicleId = await factory.CreateLiveVehicleAsync(partner, typeId, locationId);

        Assert.Equal("AvailableNow", await AvailabilityAsync(admin, partnerId, vehicleId));

        (await partner.PutAsJsonAsync($"/api/partner/vehicles/{vehicleId}/status", new { status = "Inactive" })).EnsureSuccessStatusCode();
        Assert.Equal("SwitchedOff", await AvailabilityAsync(admin, partnerId, vehicleId));

        (await partner.PutAsJsonAsync($"/api/partner/vehicles/{vehicleId}/status", new { status = "Active" })).EnsureSuccessStatusCode();
        Assert.Equal("AvailableNow", await AvailabilityAsync(admin, partnerId, vehicleId));
    }

    [Fact]
    public async Task Dashboard_is_admin_only()
    {
        using var customer = await factory.CustomerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/dashboard")).StatusCode);

        using var anonymous = factory.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/dashboard")).StatusCode);
    }

    private static async Task<string?> AvailabilityAsync(HttpClient admin, int partnerId, int vehicleId)
    {
        var dashboard = await admin.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
        var partner = dashboard.GetProperty("partners").EnumerateArray().Single(p => p.GetProperty("rentalPartnerId").GetInt32() == partnerId);
        var vehicle = partner.GetProperty("vehicles").EnumerateArray().Single(v => v.GetProperty("partnerVehicleId").GetInt32() == vehicleId);
        return vehicle.GetProperty("availability").GetString();
    }
}
