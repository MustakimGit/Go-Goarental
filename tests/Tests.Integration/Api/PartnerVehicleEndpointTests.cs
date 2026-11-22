using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace GoGoaRentals.Tests.Integration.Api;

public class PartnerVehicleEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n% test document\n%%EOF");

    private static readonly string[] AllDocuments = ["RegistrationCertificate", "Insurance", "PollutionCertificate", "RentalPermit"];

    private static string NextYear => DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Partner_awaiting_approval_cannot_add_vehicles()
    {
        var (partner, _) = await factory.PartnerClientAsync(approve: false);
        using var _ = partner;

        var response = await partner.PostAsJsonAsync("/api/partner/vehicles", await AdminEndpointTests.VehicleRequestAsync(partner));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Make_model_and_type_come_from_the_catalog_choice()
    {
        var (partner, _) = await factory.PartnerClientAsync(approve: true);
        using var _ = partner;
        var models = await partner.GetFromJsonAsync<JsonElement>("/api/catalog/vehicle-models");
        var activa = models.EnumerateArray().Single(m => m.GetProperty("make").GetString() == "Honda" && m.GetProperty("model").GetString() == "Activa 6G");
        Assert.Equal("Non Gear", activa.GetProperty("vehicleTypeName").GetString());
        var location = (await partner.GetFromJsonAsync<JsonElement>("/api/catalog/pickup-locations"))[0].GetProperty("pickupLocationId").GetInt32();

        var response = await partner.PostAsJsonAsync("/api/partner/vehicles", new
        {
            vehicleId = activa.GetProperty("vehicleId").GetInt32(),
            registrationNumber = ApiFactory.UniqueRegistration(),
            manufacturingYear = 2024,
            pickupLocationId = location,
            deliveryRadiusKm = 0,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var vehicle = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Honda", vehicle.GetProperty("make").GetString());
        Assert.Equal("Activa 6G", vehicle.GetProperty("model").GetString());
        Assert.Equal(activa.GetProperty("vehicleTypeId").GetInt32(), vehicle.GetProperty("vehicleTypeId").GetInt32());
    }

    [Fact]
    public async Task Unknown_or_inactive_catalog_model_is_rejected()
    {
        var (partner, _) = await factory.PartnerClientAsync(approve: true);
        using var _ = partner;
        using var admin = await factory.AdminClientAsync();
        var typeId = (await admin.GetFromJsonAsync<JsonElement>("/api/catalog/vehicle-types"))[0].GetProperty("vehicleTypeId").GetInt32();
        var created = await admin.PostAsJsonAsync("/api/admin/vehicle-models", new { vehicleTypeId = typeId, make = "Test", model = $"Off {Guid.NewGuid():N}"[..20], isActive = false });
        created.EnsureSuccessStatusCode();
        var inactiveId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("vehicleId").GetInt32();
        var location = (await partner.GetFromJsonAsync<JsonElement>("/api/catalog/pickup-locations"))[0].GetProperty("pickupLocationId").GetInt32();

        foreach (var vehicleId in new[] { inactiveId, int.MaxValue })
        {
            var response = await partner.PostAsJsonAsync("/api/partner/vehicles", new
            {
                vehicleId, registrationNumber = ApiFactory.UniqueRegistration(), manufacturingYear = 2024, pickupLocationId = location, deliveryRadiusKm = 0,
            });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }
    }

    [Fact]
    public async Task Vehicle_goes_live_only_after_all_documents_are_verified()
    {
        var (partner, _) = await factory.PartnerClientAsync(approve: true);
        using var _ = partner;
        using var admin = await factory.AdminClientAsync();

        var vehicle = await CreateVehicleAsync(partner);
        Assert.Equal("PendingVerification", vehicle.GetProperty("status").GetString());
        var vehicleId = vehicle.GetProperty("partnerVehicleId").GetInt32();

        foreach (var type in AllDocuments)
        {
            vehicle = await UploadAsync(partner, vehicleId, type, type == "RegistrationCertificate" ? null : NextYear);
        }

        var documentIds = vehicle.GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("vehicleDocumentId").GetInt32()).ToList();
        Assert.Equal(4, documentIds.Count);

        var queue = await admin.GetFromJsonAsync<JsonElement>("/api/admin/vehicles?needsReview=true");
        Assert.Contains(queue.EnumerateArray(), v => v.GetProperty("partnerVehicleId").GetInt32() == vehicleId);

        JsonElement reviewed = default;
        foreach (var (documentId, index) in documentIds.Select((id, i) => (id, i)))
        {
            reviewed = await ReviewAsync(admin, documentId, approve: true);
            var expected = index < documentIds.Count - 1 ? "PendingVerification" : "Active";
            Assert.Equal(expected, reviewed.GetProperty("status").GetString());
        }

        Assert.Equal("Verified", reviewed.GetProperty("documentVerificationStatus").GetString());
    }

    [Fact]
    public async Task Rejected_document_shows_the_reason_to_the_partner()
    {
        var (partner, _) = await factory.PartnerClientAsync(approve: true);
        using var _ = partner;
        using var admin = await factory.AdminClientAsync();

        var vehicleId = (await CreateVehicleAsync(partner)).GetProperty("partnerVehicleId").GetInt32();
        var uploaded = await UploadAsync(partner, vehicleId, "Insurance", NextYear);
        var documentId = uploaded.GetProperty("documents")[0].GetProperty("vehicleDocumentId").GetInt32();

        var noReason = await admin.PutAsJsonAsync($"/api/admin/vehicle-documents/{documentId}/review", new { approve = false });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noReason.StatusCode);

        await ReviewAsync(admin, documentId, approve: false, reason: "Policy number unreadable");

        var mine = await partner.GetFromJsonAsync<JsonElement>($"/api/partner/vehicles/{vehicleId}");
        Assert.Equal("Rejected", mine.GetProperty("documentVerificationStatus").GetString());
        Assert.Equal("Policy number unreadable", mine.GetProperty("documents")[0].GetProperty("reviewNote").GetString());
    }

    [Fact]
    public async Task Partners_cannot_see_each_others_vehicles_or_documents()
    {
        var (owner, _) = await factory.PartnerClientAsync(approve: true);
        var (other, _) = await factory.PartnerClientAsync(approve: true);
        using var _1 = owner;
        using var _2 = other;

        var vehicleId = (await CreateVehicleAsync(owner)).GetProperty("partnerVehicleId").GetInt32();
        var uploaded = await UploadAsync(owner, vehicleId, "Insurance", NextYear);
        var documentId = uploaded.GetProperty("documents")[0].GetProperty("vehicleDocumentId").GetInt32();

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/partner/vehicles/{vehicleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/partner/documents/{documentId}/file")).StatusCode);
        Assert.DoesNotContain((await other.GetFromJsonAsync<JsonElement>("/api/partner/vehicles")).EnumerateArray(),
            v => v.GetProperty("partnerVehicleId").GetInt32() == vehicleId);

        var download = await owner.GetAsync($"/api/partner/documents/{documentId}/file");
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PdfBytes, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Registration_number_is_normalized_and_must_be_unique()
    {
        var (partner, _) = await factory.PartnerClientAsync(approve: true);
        using var _ = partner;
        var plate = ApiFactory.UniqueRegistration();
        var spaced = $"{plate[..2]} {plate[2..4]} {plate[4..6]}-{plate[6..]}".ToLowerInvariant();

        var created = await CreateVehicleAsync(partner, spaced);
        Assert.Equal(plate, created.GetProperty("registrationNumber").GetString());

        var duplicate = await partner.PostAsJsonAsync("/api/partner/vehicles", await AdminEndpointTests.VehicleRequestAsync(partner, plate));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Uploads_must_be_a_real_pdf_or_image_and_need_an_expiry_date()
    {
        var (partner, _) = await factory.PartnerClientAsync(approve: true);
        using var _ = partner;
        var vehicleId = (await CreateVehicleAsync(partner)).GetProperty("partnerVehicleId").GetInt32();

        var notAPdf = await PostDocumentAsync(partner, vehicleId, "Insurance", NextYear, Encoding.UTF8.GetBytes("just text"), "fake.pdf");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notAPdf.StatusCode);

        var noExpiry = await PostDocumentAsync(partner, vehicleId, "Insurance", null, PdfBytes, "insurance.pdf");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noExpiry.StatusCode);

        var expired = await PostDocumentAsync(partner, vehicleId, "Insurance", "2020-01-01", PdfBytes, "insurance.pdf");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, expired.StatusCode);
    }

    [Fact]
    public async Task Partner_can_pause_and_retire_a_vehicle()
    {
        var (partner, _) = await factory.PartnerClientAsync(approve: true);
        using var _ = partner;
        var vehicleId = (await CreateVehicleAsync(partner)).GetProperty("partnerVehicleId").GetInt32();

        var paused = await partner.PutAsJsonAsync($"/api/partner/vehicles/{vehicleId}/status", new { status = "Inactive" });
        Assert.Equal("Inactive", (await paused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        // Without verified documents, switching back on waits for checks rather than going live.
        var resumed = await partner.PutAsJsonAsync($"/api/partner/vehicles/{vehicleId}/status", new { status = "Active" });
        Assert.Equal("PendingVerification", (await resumed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        (await partner.PutAsJsonAsync($"/api/partner/vehicles/{vehicleId}/status", new { status = "Retired" })).EnsureSuccessStatusCode();
        var afterRetire = await partner.PutAsJsonAsync($"/api/partner/vehicles/{vehicleId}/status", new { status = "Active" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, afterRetire.StatusCode);
    }

    private static async Task<JsonElement> CreateVehicleAsync(HttpClient partner, string? registration = null)
    {
        var response = await partner.PostAsJsonAsync("/api/partner/vehicles", await AdminEndpointTests.VehicleRequestAsync(partner, registration));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> UploadAsync(HttpClient partner, int vehicleId, string type, string? expiresOn)
    {
        var response = await PostDocumentAsync(partner, vehicleId, type, expiresOn, PdfBytes, $"{type}.pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> PostDocumentAsync(HttpClient partner, int vehicleId, string type, string? expiresOn, byte[] bytes, string fileName)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent
        {
            { new StringContent(type), "documentType" },
            { file, "file", fileName },
        };
        if (expiresOn is not null)
        {
            form.Add(new StringContent(expiresOn), "expiresOn");
        }

        return partner.PostAsync($"/api/partner/vehicles/{vehicleId}/documents", form);
    }

    private static async Task<JsonElement> ReviewAsync(HttpClient admin, int documentId, bool approve, string? reason = null)
    {
        var response = await admin.PutAsJsonAsync($"/api/admin/vehicle-documents/{documentId}/review", new { approve, reason });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
