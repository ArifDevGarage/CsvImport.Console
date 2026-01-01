using System.Globalization;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CsvImport.ConsoleApp;

public sealed class ExtEmployeeFromSintumImportHandler : IImportHandler
{
    // use this with: dotnet run -- --entity extemployee --file "..."
    public string EntityName => "extemployee";

    private readonly ImportDbContext _db;
    private readonly ImportSettings _settings;
    private readonly ILogger<ExtEmployeeFromSintumImportHandler> _logger;

    public ExtEmployeeFromSintumImportHandler(
        ImportDbContext db,
        IOptions<ImportSettings> settings,
        ILogger<ExtEmployeeFromSintumImportHandler> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task RunAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("CSV file not found.", filePath);

        // If you do UPSERT (updates), EF needs DetectChanges to pick updates when AutoDetectChanges is false.
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        var batchSize = Math.Max(1, _settings.BatchSize);
        var batch = new List<ExtEmployeeFromSintum>(capacity: batchSize);

        int readCount = 0;
        int inserted = 0;
        int skipped = 0;
        int updated = 0;

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Context.RegisterClassMap<ExtEmployeeFromSintumCsvMap>();

        // tolerance options if needed:
        // csv.Context.MissingFieldFound = null;
        // csv.Context.HeaderValidated = null;

        await foreach (var row in ReadRowsAsync(csv, ct))
        {
            readCount++;

            var employeeId = Clean(row.EmployeeId);
            var positionId = Clean(row.PositionId);

            // required keys for stable upsert
            if (string.IsNullOrWhiteSpace(employeeId) || string.IsNullOrWhiteSpace(positionId))
            {
                skipped++;
                continue;
            }

            // --- UPSERT: update if exists, else insert ---
            // (Simple approach: per-row query. Works, but slower for huge files.)
            var existing = await _db.ExtEmployeeFromSintums
                .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.PositionId == positionId, ct);

            if (existing != null)
            {
                ApplyRow(existing, row);
                updated++;
            }
            else
            {
                var entity = new ExtEmployeeFromSintum
                {
                    EmployeeId = employeeId,
                    PositionId = positionId
                };

                ApplyRow(entity, row);
                batch.Add(entity);
            }

            if (batch.Count >= batchSize)
            {
                inserted += await FlushAsync(batch, ct);
                _logger.LogInformation(
                    "Progress: read={Read}, inserted={Inserted}, updated={Updated}, skipped={Skipped}",
                    readCount, inserted, updated, skipped
                );
            }
        }

        inserted += await FlushAsync(batch, ct);

        _logger.LogInformation(
            "DONE: read={Read}, inserted={Inserted}, updated={Updated}, skipped={Skipped}",
            readCount, inserted, updated, skipped
        );
    }

    private async Task<int> FlushAsync(List<ExtEmployeeFromSintum> batch, CancellationToken ct)
    {
        // IMPORTANT: because AutoDetectChangesEnabled = false, force detection for updates.
        _db.ChangeTracker.DetectChanges();

        if (batch.Count > 0)
            await _db.ExtEmployeeFromSintums.AddRangeAsync(batch, ct);

        await _db.SaveChangesAsync(ct);

        var inserted = batch.Count;
        batch.Clear();

        _db.ChangeTracker.Clear();
        return inserted;
    }

    private static void ApplyRow(ExtEmployeeFromSintum target, ExtEmployeeFromSintumCsvRow row)
    {
        target.EmployeeName = Clean(row.EmployeeName);

        target.PositionName = Clean(row.PositionName);

        target.Area = Clean(row.Area);
        target.PlantArea = Clean(row.PlantArea);
        target.Directorate = Clean(row.Directorate);
        target.Function = Clean(row.Function);
        target.Department = Clean(row.Department);

        target.Email = Clean(row.Email);
        target.Level = Clean(row.Level);

        target.SuperiorId = Clean(row.SuperiorId);
        target.SuperiorPositionId = Clean(row.SuperiorPositionId);

        target.UserName = Clean(row.UserName);
        target.Unit = Clean(row.Unit);
        target.Posgrd = Clean(row.Posgrd);

        target.CostCenter = Clean(row.CostCenter);
        target.Entity = Clean(row.Entity);

        target.LastUpdate = ParseNullableDateTime(row.LastUpdate);
        target.HelperIsDelegate = ParseNullableBool(row.HelperIsDelegate);
        target.HelperEmployeePositionTypeId = ParseNullableByte(row.HelperEmployeePositionTypeId);
    }

    private static string? Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return s.Trim();
    }

    private static DateTime? ParseNullableDateTime(string? s)
    {
        s = Clean(s);
        if (s is null) return null;

        // Try common formats first; fallback to general parse.
        if (DateTime.TryParseExact(s,
                new[]
                {
                    "yyyy-MM-dd HH:mm:ss",
                    "yyyy-MM-dd",
                    "MM/dd/yyyy",
                    "MM/dd/yyyy HH:mm:ss",
                    "dd/MM/yyyy",
                    "dd/MM/yyyy HH:mm:ss"
                },
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var dtExact))
            return dtExact;

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            return dt;

        return null;
    }

    private static bool? ParseNullableBool(string? s)
    {
        s = Clean(s);
        if (s is null) return null;

        // accept: true/false, 1/0, Y/N, Yes/No
        if (bool.TryParse(s, out var b)) return b;

        if (s == "1" || s.Equals("Y", StringComparison.OrdinalIgnoreCase) || s.Equals("YES", StringComparison.OrdinalIgnoreCase))
            return true;

        if (s == "0" || s.Equals("N", StringComparison.OrdinalIgnoreCase) || s.Equals("NO", StringComparison.OrdinalIgnoreCase))
            return false;

        return null;
    }

    private static byte? ParseNullableByte(string? s)
    {
        s = Clean(s);
        if (s is null) return null;

        if (byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return v;

        return null;
    }

    private static async IAsyncEnumerable<ExtEmployeeFromSintumCsvRow> ReadRowsAsync(
        CsvReader csv,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var r in csv.GetRecords<ExtEmployeeFromSintumCsvRow>())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Yield();
        }
    }
}