using System.Globalization;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CsvImport.ConsoleApp;

public sealed class AspNetRoleClaimImportHandler : IImportHandler
{
    public string EntityName => "roleclaim"; // run with: --entity roleclaim

    private readonly ImportDbContext _db;
    private readonly ImportSettings _settings;
    private readonly ILogger<AspNetRoleClaimImportHandler> _logger;

    // Cache to reduce DB queries
    private readonly HashSet<string> _knownValidRoleIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _knownInvalidRoleIds = new(StringComparer.Ordinal);

    // Key cache per RoleId to skip duplicates quickly:
    // RoleId -> HashSet("ClaimType|ClaimValue")
    private readonly Dictionary<string, HashSet<string>> _existingClaimKeysByRoleId = new(StringComparer.Ordinal);

    public AspNetRoleClaimImportHandler(
        ImportDbContext db,
        IOptions<ImportSettings> settings,
        ILogger<AspNetRoleClaimImportHandler> logger)
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
        var batch = new List<AspNetRoleClaim>(capacity: batchSize);

        int readCount = 0;
        int inserted = 0;
        int skipped = 0;
        int invalid = 0;
        int duplicates = 0;

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<AspNetRoleClaimCsvMap>();

        await foreach (var row in ReadRowsAsync(csv, ct))
        {
            readCount++;

            var roleId = (row.RoleId ?? "").Trim();
            var claimType = (row.ClaimType ?? "").Trim();
            var claimValue = string.IsNullOrWhiteSpace(row.ClaimValue) ? null : row.ClaimValue.Trim();

            // Basic validation (RoleId & ClaimType are effectively required for meaningful claims)
            if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(claimType))
            {
                invalid++;
                continue;
            }

            // FK check: RoleId must exist in AspNetRoles
            if (!await IsRoleIdValidAsync(roleId, ct))
            {
                invalid++;
                continue;
            }

            // Load existing claim keys for this role (once per RoleId)
            var existingKeys = await GetOrLoadExistingClaimKeysForRoleAsync(roleId, ct);

            var key = BuildClaimKey(claimType, claimValue);
            if (existingKeys.Contains(key))
            {
                duplicates++;
                skipped++;
                continue;
            }

            // Add to batch + mark as existing (so duplicates in same CSV are also skipped)
            existingKeys.Add(key);

            batch.Add(new AspNetRoleClaim
            {
                RoleId = roleId,
                ClaimType = claimType,
                ClaimValue = claimValue
            });

            if (batch.Count >= batchSize)
            {
                inserted += await FlushAsync(batch, ct);
                _logger.LogInformation(
                    "Progress: read={Read}, inserted={Inserted}, skipped={Skipped}, invalid={Invalid}, duplicates={Duplicates}",
                    readCount, inserted, skipped, invalid, duplicates);
            }
        }

        inserted += await FlushAsync(batch, ct);

        _logger.LogInformation(
            "DONE: read={Read}, inserted={Inserted}, skipped={Skipped}, invalid={Invalid}, duplicates={Duplicates}",
            readCount, inserted, skipped, invalid, duplicates);
    }

    private async Task<int> FlushAsync(List<AspNetRoleClaim> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
            return 0;

        await _db.AspNetRoleClaims.AddRangeAsync(batch, ct);
        await _db.SaveChangesAsync(ct);

        var inserted = batch.Count;
        batch.Clear();
        _db.ChangeTracker.Clear();

        return inserted;
    }

    private async Task<bool> IsRoleIdValidAsync(string roleId, CancellationToken ct)
    {
        if (_knownValidRoleIds.Contains(roleId)) return true;
        if (_knownInvalidRoleIds.Contains(roleId)) return false;

        var exists = await _db.AspNetRoles.AsNoTracking().AnyAsync(r => r.Id == roleId, ct);
        if (exists) _knownValidRoleIds.Add(roleId);
        else _knownInvalidRoleIds.Add(roleId);

        return exists;
    }

    private async Task<HashSet<string>> GetOrLoadExistingClaimKeysForRoleAsync(string roleId, CancellationToken ct)
    {
        if (_existingClaimKeysByRoleId.TryGetValue(roleId, out var keys))
            return keys;

        // Load existing claims for this RoleId once
        var loaded = await _db.AspNetRoleClaims
            .AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .Select(x => new { x.ClaimType, x.ClaimValue })
            .ToListAsync(ct);

        keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in loaded)
        {
            var claimType = (item.ClaimType ?? "").Trim();
            var claimValue = string.IsNullOrWhiteSpace(item.ClaimValue) ? null : item.ClaimValue.Trim();
            if (!string.IsNullOrWhiteSpace(claimType))
                keys.Add(BuildClaimKey(claimType, claimValue));
        }

        _existingClaimKeysByRoleId[roleId] = keys;
        return keys;
    }

    private static string BuildClaimKey(string claimType, string? claimValue)
        => $"{claimType}|{claimValue ?? ""}";

    private static async IAsyncEnumerable<AspNetRoleClaimCsvRow> ReadRowsAsync(
        CsvReader csv,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var r in csv.GetRecords<AspNetRoleClaimCsvRow>())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Yield();
        }
    }
}
