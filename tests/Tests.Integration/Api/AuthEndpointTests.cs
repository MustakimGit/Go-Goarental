using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GoGoaRentals.Tests.Integration.Api;

public class AuthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = ApiFactory.Password;

    private static string UniqueEmail() => ApiFactory.UniqueEmail();

    private HttpClient NewClient() => factory.NewClient();

    [Fact]
    public async Task Me_returns_401_without_a_session()
    {
        using var client = NewClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_signs_in_and_logout_ends_the_session()
    {
        using var client = NewClient();
        var email = UniqueEmail();

        var register = await client.PostAsJsonAsync("/api/auth/register", new { name = "Asha Naik", email, password = Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal("Customer", me.GetProperty("role").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_accepts_the_right_password_with_any_email_casing()
    {
        var email = UniqueEmail();
        using (var setup = NewClient())
        {
            await setup.PostAsJsonAsync("/api/auth/register", new { name = "Rohan", email, password = Password });
        }

        using var client = NewClient();
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "not-the-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var right = await client.PostAsJsonAsync("/api/auth/login", new { email = email.ToUpperInvariant(), password = Password });
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_returns_409()
    {
        using var client = NewClient();
        var email = UniqueEmail();

        await client.PostAsJsonAsync("/api/auth/register", new { name = "First", email, password = Password });
        var second = await client.PostAsJsonAsync("/api/auth/register", new { name = "Second", email, password = Password });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Partner_registration_creates_a_partner_awaiting_approval()
    {
        using var client = NewClient();
        var phone = ApiFactory.UniquePhone();

        var response = await client.PostAsJsonAsync("/api/auth/register-partner", new
        {
            name = "Sunil Gaonkar",
            email = UniqueEmail(),
            phone,
            password = Password,
            businessName = "Sunil Bike Rentals",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Partner", body.GetProperty("role").GetString());
        Assert.Equal("PendingApproval", body.GetProperty("partnerStatus").GetString());
        Assert.True(body.GetProperty("rentalPartnerId").GetInt32() > 0);
    }

    [Fact]
    public async Task Short_password_is_rejected_with_validation_errors()
    {
        using var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { name = "X", email = UniqueEmail(), password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("Password", out _));
    }
}
