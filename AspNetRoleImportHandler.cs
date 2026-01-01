
using System.Globalization;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
// using EFormGenerateDatabase.Models.DataModels; // <-- your scaffolded model namespace

namespace CsvImport.ConsoleApp;

public sealed class AspNetRoleImportHandler : IImportHandler
{
    public string EntityName => "role";

    private readonly ImportDbContext _db;
    private readonly ImportSettings _settings;
    private readonly ILogger<AspNetRoleImportHandler> _logger;

    public AspNetRoleImportHandler(
        ImportDbContext db,
        IOptions<ImportSettings> settings,
        ILogger<AspNetRoleImportHandler> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task RunAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("CSV file not found.", filePath);

        // Good for bulk inserts, but be careful: updates won't be auto-detected.
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        var batchSize = Math.Max(1, _settings.BatchSize);
        var batch = new List<AspNetRoleCsvRow>(capacity: batchSize);

        int readCount = 0;
        int inserted = 0;
        int updated = 0;
        int skipped = 0;

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Context.RegisterClassMap<AspNetRoleCsvMap>();

        await foreach (var row in ReadRowsAsync(csv, ct))
        {
            readCount++;

            // basic validation
            var name = (row.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                skipped++;
                continue;
            }

            batch.Add(row);

            if (batch.Count >= batchSize)
            {
                var (ins, upd) = await FlushBatchAsync(batch, ct);
                inserted += ins;
                updated += upd;

                _logger.LogInformation(
                    "Progress: read={Read}, inserted={Inserted}, updated={Updated}, skipped={Skipped}",
                    readCount, inserted, updated, skipped);
            }
        }

        {
            var (ins, upd) = await FlushBatchAsync(batch, ct);
            inserted += ins;
            updated += upd;
        }

        _logger.LogInformation(
            "DONE: read={Read}, inserted={Inserted}, updated={Updated}, skipped={Skipped}",
            readCount, inserted, updated, skipped);
    }

    private async Task<(int Inserted, int Updated)> FlushBatchAsync(List<AspNetRoleCsvRow> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
            return (0, 0);

        // Prepare keys (NormalizedName) for one DB roundtrip per batch
        var normalizedKeys = batch
            .Select(x => NormalizeRoleName(x.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Load existing roles by NormalizedName (Identity convention)
        var existing = await _db.Set<AspNetRole>()
            .Where(r => r.NormalizedName != null && normalizedKeys.Contains(r.NormalizedName))
            .ToListAsync(ct);

        var existingByNormalized = existing
            .Where(r => r.NormalizedName != null)
            .ToDictionary(r => r.NormalizedName!, StringComparer.Ordinal);

        int inserted = 0;
        int updated = 0;

        foreach (var row in batch)
        {
            ct.ThrowIfCancellationRequested();

            var name = (row.Name ?? "").Trim();
            var normalized = NormalizeRoleName(name);

            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            // allow optional id + concurrency stamp from csv
            var id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString() : row.Id.Trim();
            var stamp = string.IsNullOrWhiteSpace(row.ConcurrencyStamp) ? Guid.NewGuid().ToString() : row.ConcurrencyStamp.Trim();

            if (existingByNormalized.TryGetValue(normalized, out var role))
            {
                // UPDATE
                role.Name = name;
                role.NormalizedName = normalized;

                // usually safe to refresh stamp when importing
                role.ConcurrencyStamp = stamp;

                // IMPORTANT: AutoDetectChanges is off -> force modified
                _db.Entry(role).State = EntityState.Modified;

                updated++;
            }
            else
            {
                // INSERT
                role = new AspNetRole
                {
                    Id = id,
                    Name = name,
                    NormalizedName = normalized,
                    ConcurrencyStamp = stamp
                };

                await _db.Set<AspNetRole>().AddAsync(role, ct);

                // add to dict so duplicates in the same batch don't attempt duplicate inserts
                existingByNormalized[normalized] = role;

                inserted++;
            }
        }

        await _db.SaveChangesAsync(ct);

        batch.Clear();

        // Keep memory stable
        _db.ChangeTracker.Clear();

        return (inserted, updated);
    }

    private static string NormalizeRoleName(string? name)
        => (name ?? "").Trim().ToUpperInvariant();

    private static async IAsyncEnumerable<AspNetRoleCsvRow> ReadRowsAsync(
        CsvReader csv,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var r in csv.GetRecords<AspNetRoleCsvRow>())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Yield();
        }
    }
}
