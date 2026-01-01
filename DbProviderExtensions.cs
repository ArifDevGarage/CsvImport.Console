using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CsvImport.ConsoleApp;

public static class DbProviderExtensions
{
    public static void UseConfiguredProvider(this DbContextOptionsBuilder options, ImportSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("Import:ConnectionString is empty.");

        var provider = (settings.Provider ?? "").Trim();

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlServer(settings.ConnectionString);
            return;
        }

        if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            options.UseNpgsql(settings.ConnectionString);
            return;
        }

        if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("MariaDb", StringComparison.OrdinalIgnoreCase))
        {
            // Pomelo needs server version (AutoDetect is easiest)
            options.UseMySql(settings.ConnectionString, ServerVersion.AutoDetect(settings.ConnectionString));
            return;
        }

        throw new NotSupportedException($"Unsupported provider '{settings.Provider}'. Use SqlServer | Postgres | MySql.");
    }
}
