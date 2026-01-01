using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CsvImport.ConsoleApp;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Very simple CLI:
        // dotnet run -- --entity customer --file "C:\temp\customers.csv"
        var cli = CliOptions.Parse(args);
        if (!cli.IsValid)
        {
            System.Console.WriteLine("Usage: --entity <name> --file <path>");
            System.Console.WriteLine("Example: dotnet run -- --entity customer --file \"C:\\temp\\customers.csv\"");
            return 2;
        }

        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                cfg.AddEnvironmentVariables();
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<ImportSettings>(ctx.Configuration.GetSection("Import"));

                services.AddDbContext<ImportDbContext>((sp, options) =>
                {
                    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ImportSettings>>().Value;
                    options.UseConfiguredProvider(settings);
                });

                // Register import handlers (add more handlers later)
                services.AddScoped<IImportHandler, CustomerImportHandler>();
                services.AddScoped<IImportHandler, AspNetUserImportHandler>();
                services.AddScoped<IImportHandler, AspNetRoleImportHandler>();
                services.AddScoped<IImportHandler, AspNetRoleClaimImportHandler>();
                services.AddScoped<IImportHandler, ExtEmployeeFromSintumImportHandler>();

                


                services.AddScoped<ImportDispatcher>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .Build();

        // Optional: ensure DB exists (for demo). For production, you likely migrate externally.
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ImportDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        using (var scope = host.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<ImportDispatcher>();
            await dispatcher.RunAsync(cli.Entity!, cli.FilePath!, CancellationToken.None);
        }

        return 0;
    }
}
