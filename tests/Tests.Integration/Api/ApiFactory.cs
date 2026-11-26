using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GoGoaRentals.Tests.Integration.Api;

/// <summary>The API wired to the test database, with uploads in a temp folder.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Test-only admin, inserted into the test database by TestDatabase.SeedTestAdmin.
    public const string AdminEmail = "admin@tests.local";
    public const string AdminPassword = "Admin@12345";
    public const string Password = "Correct-Horse-9";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestDatabase.ConnectionString);
        builder.UseSetting("Storage:LocalRoot", Path.Combine(Path.GetTempPath(), "gogoa-test-uploads"));
    }

    /// <summary>A client that keeps the auth cookie between requests, like a browser.</summary>
    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    public async Task<HttpClient> AdminClientAsync()
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = AdminEmail, password = AdminPassword });
        response.EnsureSuccessStatusCode();
        return client;
    }

    public async Task<HttpClient> CustomerClientAsync()
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new { name = "Test Customer", email = UniqueEmail(), password = Password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Registers a partner; with approve, an admin approves them and the client logs in again.</summary>
    public async Task<(HttpClient Client, int PartnerId)> PartnerClientAsync(bool approve)
    {
        var client = NewClient();
        var email = UniqueEmail();
        var response = await client.PostAsJsonAsync("/api/auth/register-partner", new
        {
            name = "Test Partner",
            email,
            phone = UniquePhone(),
            password = Password,
            businessName = $"Test Rentals {Guid.NewGuid():N}"[..24],
        });
        response.EnsureSuccessStatusCode();
        var partnerId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rentalPartnerId").GetInt32();

        if (approve)
        {
            using var admin = await AdminClientAsync();
            (await admin.PutAsJsonAsync($"/api/admin/partners/{partnerId}/status", new { status = "Active" })).EnsureSuccessStatusCode();
        }

        return (client, partnerId);
    }

    /// <summary>
    /// A catalog make + model (dbo.Vehicle) of the given type, for partners to pick. Tests often create their own
    /// vehicle types, so a model is added to the catalog when the type has none yet.
    /// </summary>
    public async Task<int> CatalogModelIdAsync(int vehicleTypeId)
    {
        using var admin = await AdminClientAsync();
        var models = await admin.GetFromJsonAsync<JsonElement>("/api/admin/vehicle-models");
        foreach (var m in models.EnumerateArray())
        {
            if (m.GetProperty("vehicleTypeId").GetInt32() == vehicleTypeId && m.GetProperty("isActive").GetBoolean())
            {
                return m.GetProperty("vehicleId").GetInt32();
            }
        }

        var created = await admin.PostAsJsonAsync("/api/admin/vehicle-models", new { vehicleTypeId, make = "Test", model = $"Model {Guid.NewGuid():N}"[..20] });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("vehicleId").GetInt32();
    }

    /// <summary>Adds a vehicle for the partner and has an admin verify all four documents, so it is live.</summary>
    public async Task<int> CreateLiveVehicleAsync(HttpClient partner, int vehicleTypeId, int pickupLocationId)
    {
        var created = await partner.PostAsJsonAsync("/api/partner/vehicles", new
        {
            vehicleId = await CatalogModelIdAsync(vehicleTypeId),
            registrationNumber = UniqueRegistration(),
            manufacturingYear = 2024,
            pickupLocationId,
            deliveryRadiusKm = 5,
        });
        created.EnsureSuccessStatusCode();
        var vehicleId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("partnerVehicleId").GetInt32();

        var expires = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd");
        foreach (var type in new[] { "RegistrationCertificate", "Insurance", "PollutionCertificate", "RentalPermit" })
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(type), "documentType" },
                { new ByteArrayContent("%PDF-1.4 test"u8.ToArray()), "file", $"{type}.pdf" },
            };
            if (type != "RegistrationCertificate")
            {
                form.Add(new StringContent(expires), "expiresOn");
            }

            (await partner.PostAsync($"/api/partner/vehicles/{vehicleId}/documents", form)).EnsureSuccessStatusCode();
        }

        using var admin = await AdminClientAsync();
        var vehicle = await partner.GetFromJsonAsync<JsonElement>($"/api/partner/vehicles/{vehicleId}");
        foreach (var doc in vehicle.GetProperty("documents").EnumerateArray())
        {
            var id = doc.GetProperty("vehicleDocumentId").GetInt32();
            (await admin.PutAsJsonAsync($"/api/admin/vehicle-documents/{id}/review", new { approve = true })).EnsureSuccessStatusCode();
        }

        return vehicleId;
    }

    public static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.test";

    public static string UniquePhone() => $"9{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

    /// <summary>A random Goa-style plate, e.g. GA07AB1234.</summary>
    public static string UniqueRegistration()
    {
        const string letters = "ABCDEFGHJKLMNPRSTUVWXYZ";
        return $"GA{Random.Shared.Next(1, 13):00}{letters[Random.Shared.Next(letters.Length)]}{letters[Random.Shared.Next(letters.Length)]}{Random.Shared.Next(1000, 10000)}";
    }
}
