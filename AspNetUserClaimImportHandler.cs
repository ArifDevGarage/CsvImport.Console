using System.Globalization;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CsvImport.ConsoleApp;

public sealed class AspNetUserClaimImportHandler : IImportHandler
{
    public string EntityName => "aspnetuserclaim"; // dotnet run -- --entity aspnetuserclaim --file "..."

    private readonly ImportDbContext _db;
    private readonly ImportSettings _settings;
    private readonly ILogger<AspNetUserClaimImportHandler> _logger;

    // cache to avoid checking the same userId repeatedly
    private readonly HashSet<string> _knownUserIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingUserIds = new(StringComparer.Ordinal);

    public AspNetUserClaimImportHandler(
        ImportDbContext db,
        IOptions<ImportSettings> settings,
        ILogger<AspNetUserClaimImportHandler> logger)
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
        var batch = new List<AspNetUserClaim>(capacity: batchSize);

        int readCount = 0;
        int inserted = 0;
        int updated = 0;
        int skipped = 0;

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Context.RegisterClassMap<AspNetUserClaimCsvMap>();

        await foreach (var row in ReadRowsAsync(csv, ct))
        {
            readCount++;

            var userId = (row.UserId ?? "").Trim();
            var claimType = (row.ClaimType ?? "").Trim();
            var claimValue = string.IsNullOrWhiteSpace(row.ClaimValue) ? null : row.ClaimValue.Trim();

            // validation
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(claimType))
            {
                skipped++;
                continue;
            }

            // ensure FK user exists (skip if missing)
            if (!await UserExistsAsync(userId, ct))
            {
                skipped++;
                continue;
            }

            // upsert by (UserId + ClaimType)
            var existing = await _db.AspNetUserClaims
                .FirstOrDefaultAsync(x => x.UserId == userId && x.ClaimType == claimType, ct);

            if (existing != null)
            {
                existing.ClaimValue = claimValue;
                updated++;
            }
            else
            {
                batch.Add(new AspNetUserClaim
                {
                    UserId = userId,
                    ClaimType = claimType,
                    ClaimValue = claimValue
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

    private async Task<bool> UserExistsAsync(string userId, CancellationToken ct)
    {
        if (_knownUserIds.Contains(userId)) return true;
        if (_missingUserIds.Contains(userId)) return false;

        var exists = await _db.AspNetUsers.AnyAsync(u => u.Id == userId, ct);
        if (exists) _knownUserIds.Add(userId);
        else _missingUserIds.Add(userId);

        return exists;
    }

    private async Task<int> FlushAsync(List<AspNetUserClaim> batch, CancellationToken ct)
    {
        if (batch.Count > 0)
        {
            await _db.AspNetUserClaims.AddRangeAsync(batch, ct);
        }

        // IMPORTANT because AutoDetectChangesEnabled = false
        _db.ChangeTracker.DetectChanges();

        await _db.SaveChangesAsync(ct);

        var inserted = batch.Count;
        batch.Clear();

        _db.ChangeTracker.Clear();
        return inserted;
    }

    private static async IAsyncEnumerable<AspNetUserClaimCsvRow> ReadRowsAsync(
        CsvReader csv,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var r in csv.GetRecords<AspNetUserClaimCsvRow>())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Yield();
        }
    }
}
