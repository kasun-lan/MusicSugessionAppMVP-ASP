using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MusicSugessionAppMVP_ASP.Persistance;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string? connectionString = null)
    {
        // If a connection string is explicitly provided, use it.
        // Otherwise fall back to the hard-coded connection string in Persistence.OnConfiguring.
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<Persistence>(options =>
                options.UseSqlServer(connectionString));
        }
        else
        {
            services.AddDbContext<Persistence>();
        }

        return services;
    }
}


