using BranchBooking.Application.Abstractions;
using BranchBooking.Infrastructure.Persistence;
using BranchBooking.Infrastructure.Persistence.Repositories;
using BranchBooking.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BranchBooking.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure services. The connection string is read lazily from the container's
    /// <see cref="IConfiguration"/> and validated when the host starts, so a missing value stops the
    /// process immediately with a clear message. The <paramref name="configuration"/> argument is kept
    /// for adapters that need eager configuration (the Brevo toggle in WI-402).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure<IConfiguration>((options, config) =>
                options.ConnectionString = config.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"ConnectionStrings:{DatabaseOptions.ConnectionStringName} must be set " +
                "(environment variable ConnectionStrings__BranchBooking or user-secrets; see docs/engineering/local-postgres.md).")
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return new NpgsqlDataSourceBuilder(options.ConnectionString).Build();
        });

        services.AddDbContext<BranchBookingDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

        services.AddSingleton<IDatabaseProbe, NpgsqlDatabaseProbe>();

        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IAppointmentTypeRepository, AppointmentTypeRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ReferenceDataSeeder>();

        return services;
    }
}
