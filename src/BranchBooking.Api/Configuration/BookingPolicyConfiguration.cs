using BranchBooking.Application.Bookings;

namespace BranchBooking.Api.Configuration;

public static class BookingPolicyConfiguration
{
    /// <summary>Binds the <c>BookingPolicy</c> section and refuses to start when a value is out of range.</summary>
    public static IServiceCollection AddBookingPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BookingPolicyOptions>()
            .Bind(configuration.GetSection(BookingPolicyOptions.SectionName))
            .Validate(options => options.IsValid(), "BookingPolicy settings are out of range; see appsettings.json for the allowed ranges.")
            .ValidateOnStart();

        return services;
    }
}
