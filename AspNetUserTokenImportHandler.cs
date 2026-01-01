using System.Globalization;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CsvImport.ConsoleApp;

public sealed class AspNetUserTokenImportHandler : IImportHandler
{
    public string EntityName => "aspnetusertoken";

    private readonly ImportDbContext _db;
    private readonly ImportSettings _settings;
    private readonly ILogger<AspNetUserTokenImportHandler> _logger;

    public AspNetUserTokenImportHandler(
        ImportDbContext db,
        IOptions<ImportSettings> settings,
        ILogger<AspNetUserTokenImportHandler> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task RunAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("CSV file not found.", filePath);

        // If you do updates, either keep AutoDetectChangesEnabled=true
        // OR explicitly mark modified properties (we do that below).
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        var batchSize = Math.Max(1, _settings.BatchSize);
        var batch = new List<AspNetUserToken>(capacity: batchSize);

        int readCount = 0;
        int inserted = 0;
        int updated = 0;
        int skipped = 0;

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Context.RegisterClassMap<AspNetUserTokenCsvMap>();

        await foreach (var row in ReadRowsAsync(csv, ct))
        {
            readCount++;

            var userId = (row.UserId ?? "").Trim();
            var loginProvider = (row.LoginProvider ?? "").Trim();
            var name = (row.Name ?? "").Trim();
            var value = string.IsNullOrWhiteSpace(row.Value) ? null : row.Value.Trim();

            // basic validation (required parts of composite key)
            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(loginProvider) ||
                string.IsNullOrWhiteSpace(name))
            {
                skipped++;
                continue;
            }

            // enforce max length (matches your EF config)
            if (loginProvider.Length > 128 || name.Length > 128)
            {
                skipped++;
                continue;
            }

            // Optional FK safety: skip if user doesn't exist
            // (prevents FK violation on insert)
            var userExists = await _db.AspNetUsers
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId, ct);

            if (!userExists)
            {
                skipped++;
                continue;
            }

            // Upsert by composite key
            var existing = await _db.AspNetUserTokens.FindAsync(
                new object[] { userId, loginProvider, name },
                ct);

            if (existing != null)
            {
                // Update only if needed
                if (!string.Equals(existing.Value, value, StringComparison.Ordinal))
                {
                    existing.Value = value;

                    // Because AutoDetectChangesEnabled = false,
                    // explicitly mark modified to ensure EF persists update:
                    _db.Entry(existing).Property(x => x.Value).IsModified = true;

                    updated++;
                }
            }
            else
            {
                batch.Add(new AspNetUserToken
                {
                    UserId = userId,
                    LoginProvider = loginProvider,
                    Name = name,
                    Value = value
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

    private async Task<int> FlushAsync(List<AspNetUserToken> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
        {
            // Still persist tracked updates
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            return 0;
        }

        await _db.AspNetUserTokens.AddRangeAsync(batch, ct);
        await _db.SaveChangesAsync(ct);

        var inserted = batch.Count;
        batch.Clear();

        _db.ChangeTracker.Clear();
        return inserted;
    }

    private static async IAsyncEnumerable<AspNetUserTokenCsvRow> ReadRowsAsync(
        CsvReader csv,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var r in csv.GetRecords<AspNetUserTokenCsvRow>())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Yield();
        }
    }
}