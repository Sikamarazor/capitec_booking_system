using BranchBooking.Application.Availability;
using BranchBooking.Application.Bookings;
using BranchBooking.Application.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace BranchBooking.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers application-layer services. <see cref="BookingPolicyOptions"/> is bound and validated by the host
    /// (see <c>AddBookingPolicy</c> in the Api project); <see cref="TimeProvider"/> is registered by the host too.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<BookingPolicy>();

        services.AddScoped<ListAppointmentTypes>();
        services.AddScoped<ListBranches>();
        services.AddScoped<GetAvailability>();

        return services;
    }
}
