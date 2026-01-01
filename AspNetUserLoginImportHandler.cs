using System.Globalization;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CsvImport.ConsoleApp;

public sealed class AspNetUserLoginImportHandler : IImportHandler
{
    // choose the name you want to pass: --entity aspnetuserlogin
    public string EntityName => "aspnetuserlogin";

    private readonly ImportDbContext _db;
    private readonly ImportSettings _settings;
    private readonly ILogger<AspNetUserLoginImportHandler> _logger;

    public AspNetUserLoginImportHandler(
        ImportDbContext db,
        IOptions<ImportSettings> settings,
        ILogger<AspNetUserLoginImportHandler> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task RunAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("CSV file not found.", filePath);

        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        var batchSize = Math.Max(1, _settings.BatchSize);
        var batch = new List<AspNetUserLogin>(capacity: batchSize);

        int readCount = 0;
        int inserted = 0;
        int updated = 0;
        int skipped = 0;

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Context.RegisterClassMap<AspNetUserLoginCsvMap>();

        await foreach (var row in ReadRowsAsync(csv, ct))
        {
            readCount++;

            var loginProvider = (row.LoginProvider ?? "").Trim();
            var providerKey = (row.ProviderKey ?? "").Trim();
            var userId = (row.UserId ?? "").Trim();
            var displayName = string.IsNullOrWhiteSpace(row.ProviderDisplayName) ? null : row.ProviderDisplayName.Trim();

            // basic validation (composite key + FK)
            if (string.IsNullOrWhiteSpace(loginProvider) ||
                string.IsNullOrWhiteSpace(providerKey) ||
                string.IsNullOrWhiteSpace(userId))
            {
                skipped++;
                continue;
            }

            // Optional: ensure user exists (safer, but extra query).
            // If you trust your CSV, you can remove this block and rely on FK constraint.
            var userExists = await _db.AspNetUsers.AnyAsync(u => u.Id == userId, ct);
            if (!userExists)
            {
                skipped++;
                continue;
            }

            // Upsert by composite PK
            var existing = await _db.AspNetUserLogins.FindAsync(
                keyValues: new object?[] { loginProvider, providerKey },
                cancellationToken: ct);

            if (existing != null)
            {
                existing.ProviderDisplayName = displayName;
                existing.UserId = userId;
                updated++;
            }
            else
            {
                batch.Add(new AspNetUserLogin
                {
                    LoginProvider = loginProvider,
                    ProviderKey = providerKey,
                    ProviderDisplayName = displayName,
                    UserId = userId
                });
            }

            if (batch.Count >= batchSize)
            {
                inserted += await FlushAsync(batch, ct);
                _logger.LogInformation(
                    "Progress: read={Read}, inserted={Inserted}, updated={Updated}, skipped={Skipped}",
                    readCount, inserted, updated, skipped);
            }
        }

        inserted += await FlushAsync(batch, ct);

        _logger.LogInformation(
            "DONE: read={Read}, inserted={Inserted}, updated={Updated}, skipped={Skipped}",
            readCount, inserted, updated, skipped);
    }

    private async Task<int> FlushAsync(List<AspNetUserLogin> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
        {
            // Persist any tracked updates
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            return 0;
        }

        await _db.AspNetUserLogins.AddRangeAsync(batch, ct);
        await _db.SaveChangesAsync(ct);

        var inserted = batch.Count;
        batch.Clear();

        _db.ChangeTracker.Clear();
        return inserted;
    }

    private static async IAsyncEnumerable<AspNetUserLoginCsvRow> ReadRowsAsync(
        CsvReader csv,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var r in csv.GetRecords<AspNetUserLoginCsvRow>())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Yield();
        }
    }
}
