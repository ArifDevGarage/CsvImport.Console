using System.Globalization;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CsvImport.ConsoleApp;

public sealed class CustomerImportHandler : IImportHandler
{
    public string EntityName => "customer";

    private readonly ImportDbContext _db;
    private readonly ImportSettings _settings;
    private readonly ILogger<CustomerImportHandler> _logger;

    public CustomerImportHandler(
        ImportDbContext db,
        IOptions<ImportSettings> settings,
        ILogger<CustomerImportHandler> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task RunAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("CSV file not found.", filePath);

        // Performance tuning for bulk-ish inserts
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        var batchSize = Math.Max(1, _settings.BatchSize);
        var batch = new List<Customer>(capacity: batchSize);

        int readCount = 0;
        int inserted = 0;
        int skipped = 0;

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Context.RegisterClassMap<CustomerCsvMap>();

        // If you want to be tolerant:
        // csv.Context.MissingFieldFound = null;
        // csv.Context.HeaderValidated = null;

        await foreach (var row in ReadRowsAsync(csv, ct))
        {
            readCount++;

            // basic validation
            var code = (row.Code ?? "").Trim();
            var name = (row.Name ?? "").Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                skipped++;
                continue;
            }

            // Optional "upsert-like" behavior (generic, but slower):
            // - Check by unique key (Code), update if exists, else insert.
            // For pure insert, comment this block and just AddRange.
            var existing = await _db.Customers.FirstOrDefaultAsync(x => x.Code == code, ct);
            if (existing != null)
            {
                existing.Name = name;
                existing.Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim();
            }
            else
            {
                batch.Add(new Customer
                {
                    Code = code,
                    Name = name,
                    Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim()
                });
            }

            if (batch.Count >= batchSize)
            {
                inserted += await FlushAsync(batch, ct);
                _logger.LogInformation("Progress: read={Read}, inserted={Inserted}, skipped={Skipped}", readCount, inserted, skipped);
            }
        }

        inserted += await FlushAsync(batch, ct);

        _logger.LogInformation("DONE: read={Read}, inserted={Inserted}, skipped={Skipped}", readCount, inserted, skipped);
    }

    private async Task<int> FlushAsync(List<Customer> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
        {
            // Even if batch is empty, we may have updates tracked from "existing" updates.
            // SaveChanges will persist those.
            var changed = await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            return 0;
        }

        await _db.Customers.AddRangeAsync(batch, ct);
        await _db.SaveChangesAsync(ct);

        var inserted = batch.Count;
        batch.Clear();

        // Clear tracked entities to keep memory stable
        _db.ChangeTracker.Clear();

        return inserted;
    }

    // CsvHelper is sync, but we can wrap iteration so the handler can remain async-friendly.
    private static async IAsyncEnumerable<CustomerCsvRow> ReadRowsAsync(CsvReader csv, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var r in csv.GetRecords<CustomerCsvRow>())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Yield(); // keeps the async pipeline responsive
        }
    }
}
