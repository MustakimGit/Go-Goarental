using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GoGoaRentals.Tests.Integration.Api;

public class AdminEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/api/admin/partners")]
    [InlineData("/api/admin/vehicle-types")]
    [InlineData("/api/admin/vehicles")]
    public async Task Admin_endpoints_reject_anonymous_customers_and_partners(string url)
    {
        using var anonymous = factory.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);

        using var customer = await factory.CustomerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(url)).StatusCode);

        var (partner, _) = await factory.PartnerClientAsync(approve: true);
        using (partner)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync(url)).StatusCode);
        }
    }

    [Fact]
    public async Task Admin_approves_then_suspends_a_partner_and_invalid_moves_are_refused()
    {
        var (partner, partnerId) = await factory.PartnerClientAsync(approve: false);
        using var _ = partner;
        using var admin = await factory.AdminClientAsync();

        var pending = await admin.GetFromJsonAsync<JsonElement>("/api/admin/partners?status=PendingApproval");
        Assert.Contains(pending.EnumerateArray(), p => p.GetProperty("rentalPartnerId").GetInt32() == partnerId);

        var approved = await admin.PutAsJsonAsync($"/api/admin/partners/{partnerId}/status", new { status = "Active" });
        Assert.Equal("Active", (await approved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        // The partner sees the approval without logging in again.
        var me = await partner.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Active", me.GetProperty("partnerStatus").GetString());

        // Active → Rejected isn't an allowed move.
        var invalid = await admin.PutAsJsonAsync($"/api/admin/partners/{partnerId}/status", new { status = "Rejected" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);

        var suspended = await admin.PutAsJsonAsync($"/api/admin/partners/{partnerId}/status", new { status = "Suspended", reason = "Test" });
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
    }

    [Fact]
    public async Task Admin_sets_partner_commission()
    {
        var (partner, partnerId) = await factory.PartnerClientAsync(approve: true);
        using var _ = partner;
        using var admin = await factory.AdminClientAsync();

        var response = await admin.PutAsJsonAsync($"/api/admin/partners/{partnerId}/commission", new { commissionPercent = 15.5m });

        Assert.Equal(15.5m, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("commissionPercent").GetDecimal());
    }

    [Fact]
    public async Task Inactive_vehicle_types_are_hidden_from_the_public_catalog()
    {
        using var admin = await factory.AdminClientAsync();
        var category = await (await admin.PostAsJsonAsync("/api/admin/vehicle-categories", new { name = $"Cat {Guid.NewGuid():N}", isActive = true }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var categoryId = category.GetProperty("vehicleCategoryId").GetInt32();

        var activeName = $"Active {Guid.NewGuid():N}";
        var inactiveName = $"Inactive {Guid.NewGuid():N}";
        (await admin.PostAsJsonAsync("/api/admin/vehicle-types", new { vehicleCategoryId = categoryId, name = activeName, seatingCapacity = 2 })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/admin/vehicle-types", new { vehicleCategoryId = categoryId, name = inactiveName, status = "Inactive" })).EnsureSuccessStatusCode();

        using var anonymous = factory.NewClient();
        var names = (await anonymous.GetFromJsonAsync<JsonElement>("/api/catalog/vehicle-types"))
            .EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();

        Assert.Contains(activeName, names);
        Assert.DoesNotContain(inactiveName, names);
    }

    [Fact]
    public async Task Duplicate_vehicle_type_name_returns_409()
    {
        using var admin = await factory.AdminClientAsync();
        var types = await admin.GetFromJsonAsync<JsonElement>("/api/admin/vehicle-types");
        var existing = types[0];

        var response = await admin.PostAsJsonAsync("/api/admin/vehicle-types", new
        {
            vehicleCategoryId = existing.GetProperty("vehicleCategoryId").GetInt32(),
            name = existing.GetProperty("name").GetString(),
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Suspended_partner_loses_access_on_the_next_request()
    {
        var (partner, partnerId) = await factory.PartnerClientAsync(approve: true);
        using var _ = partner;
        Assert.Equal(HttpStatusCode.OK, (await partner.GetAsync("/api/partner/vehicles")).StatusCode);

        using var admin = await factory.AdminClientAsync();
        (await admin.PutAsJsonAsync($"/api/admin/partners/{partnerId}/status", new { status = "Suspended" })).EnsureSuccessStatusCode();

        // Partner status is a business check (can't add vehicles); the login itself is still valid.
        var create = await partner.PostAsJsonAsync("/api/partner/vehicles", await VehicleRequestAsync(partner));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, create.StatusCode);
    }

    internal static async Task<object> VehicleRequestAsync(HttpClient client, string? registration = null)
    {
        var model = (await client.GetFromJsonAsync<JsonElement>("/api/catalog/vehicle-models"))[0];
        var location = (await client.GetFromJsonAsync<JsonElement>("/api/catalog/pickup-locations"))[0];
        return new
        {
            vehicleId = model.GetProperty("vehicleId").GetInt32(),
            registrationNumber = registration ?? ApiFactory.UniqueRegistration(),
            manufacturingYear = 2023,
            pickupLocationId = location.GetProperty("pickupLocationId").GetInt32(),
            deliveryRadiusKm = 5,
        };
    }
}
