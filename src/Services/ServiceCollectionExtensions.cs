using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Auth;
using GoGoaRentals.Services.Availability;
using GoGoaRentals.Services.Catalog;
using GoGoaRentals.Services.Dashboard;
using GoGoaRentals.Services.Fleet;
using GoGoaRentals.Services.Partners;
using GoGoaRentals.Services.Pricing;
using GoGoaRentals.Services.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GoGoaRentals.Services;

public static class ServiceCollectionExtensions
{
    /// <param name="fileStorageRoot">Absolute folder for uploaded files; must not be under the web root.</param>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, string fileStorageRoot)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFileStorage>(new LocalFileStorage(fileStorageRoot));

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPartnerAdminService, PartnerAdminService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IPartnerFleetService, PartnerFleetService>();
        services.AddScoped<IVehicleReviewService, VehicleReviewService>();
        services.AddScoped<IVehicleCalendarService, VehicleCalendarService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IPricingAdminService, PricingAdminService>();
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        return services;
    }
}
