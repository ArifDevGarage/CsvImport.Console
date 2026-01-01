using System.Globalization;
using CsvHelper;
// using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity; // Fix 1
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
// using EFormGenerateDatabase.Models.DataModels; // <-- AspNetUser, AspNetRole

namespace CsvImport.ConsoleApp;

public sealed class AspNetUserImportHandler : IImportHandler
{
    // your CLI: dotnet run -- --entity user --file "....csv"
    public string EntityName => "user";

    private readonly ImportDbContext _db;
    private readonly ImportSettings _settings;
    private readonly ILogger<AspNetUserImportHandler> _logger;

    private readonly PasswordHasher<AspNetUser> _passwordHasher = new();

    public AspNetUserImportHandler(
        ImportDbContext db,
        IOptions<ImportSettings> settings,
        ILogger<AspNetUserImportHandler> logger)
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
        var batch = new List<AspNetUser>(capacity: batchSize);

        int readCount = 0;
        int inserted = 0;
        int updated = 0;
        int skipped = 0;

        // Cache roles (NormalizedName -> RoleId) for quick lookup
        var roleIdByNormalizedName = await LoadRoleCacheAsync(ct);

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Context.RegisterClassMap<AspNetUserCsvMap>();

        // Optional tolerance:
        // csv.Context.MissingFieldFound = null;
        // csv.Context.HeaderValidated = null;

        await foreach (var row in ReadRowsAsync(csv, ct))
        {
            readCount++;

            var userName = (row.UserName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                skipped++;
                continue;
            }

            var email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim();
            var phone = string.IsNullOrWhiteSpace(row.PhoneNumber) ? null : row.PhoneNumber.Trim();

            var normalizedUserName = userName.ToUpperInvariant();
            var normalizedEmail = email?.ToUpperInvariant();

            // Decide key: if CSV Id present -> find by Id; else find by NormalizedUserName
            AspNetUser? existing = null;
            var csvId = string.IsNullOrWhiteSpace(row.Id) ? null : row.Id.Trim();

            if (!string.IsNullOrWhiteSpace(csvId))
            {
                existing = await _db.AspNetUsers.FindAsync(new object[] { csvId }, ct);
            }
            else
            {
                // If roles are present and you want safe "merge roles", include Roles
                if (!string.IsNullOrWhiteSpace(row.Roles))
                {
                    existing = await _db.AspNetUsers
                        .Include(u => u.Roles)
                        .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, ct);
                }
                else
                {
                    existing = await _db.AspNetUsers
                        .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, ct);
                }
            }

            if (existing != null)
            {
                // Update
                existing.UserName = userName;
                existing.NormalizedUserName = normalizedUserName;

                existing.Email = email;
                existing.NormalizedEmail = normalizedEmail;

                existing.PhoneNumber = phone;

                if (row.EmailConfirmed.HasValue)
                    existing.EmailConfirmed = row.EmailConfirmed.Value;

                // Password (optional)
                if (!string.IsNullOrWhiteSpace(row.Password))
                {
                    existing.PasswordHash = _passwordHasher.HashPassword(existing, row.Password.Trim());
                    existing.SecurityStamp = Guid.NewGuid().ToString();
                }

                // Update ConcurrencyStamp to mimic Identity behavior on edits (optional but recommended)
                existing.ConcurrencyStamp = Guid.NewGuid().ToString();

                // IMPORTANT when AutoDetectChangesEnabled = false:
                _db.Entry(existing).State = EntityState.Modified;

                // Roles (optional) - safe merge (won't remove existing)
                if (!string.IsNullOrWhiteSpace(row.Roles))
                {
                    await EnsureUserRolesAsync(existing, row.Roles!, roleIdByNormalizedName, ct);
                }

                updated++;
            }
            else
            {
                // Insert
                var newUser = new AspNetUser
                {
                    Id = csvId ?? Guid.NewGuid().ToString(),
                    UserName = userName,
                    NormalizedUserName = normalizedUserName,

                    Email = email,
                    NormalizedEmail = normalizedEmail,

                    PhoneNumber = phone,

                    EmailConfirmed = row.EmailConfirmed ?? false,

                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString(),

                    // other fields left default/empty:
                    // PhoneNumberConfirmed=false, TwoFactorEnabled=false, LockoutEnabled=false, etc.
                };

                // Password (optional)
                if (!string.IsNullOrWhiteSpace(row.Password))
                {
                    newUser.PasswordHash = _passwordHasher.HashPassword(newUser, row.Password.Trim());
                }

                // Roles (optional)
                if (!string.IsNullOrWhiteSpace(row.Roles))
                {
                    await EnsureUserRolesAsync(newUser, row.Roles!, roleIdByNormalizedName, ct);
                }

                batch.Add(newUser);
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

    private async Task<int> FlushAsync(List<AspNetUser> batch, CancellationToken ct)
    {
        // Insert batch (updates are already tracked)
        if (batch.Count > 0)
            await _db.AspNetUsers.AddRangeAsync(batch, ct);

        // Persist both inserts and updates
        await _db.SaveChangesAsync(ct);

        var inserted = batch.Count;
        batch.Clear();

        // keep memory stable
        _db.ChangeTracker.Clear();
        return inserted;
    }

    private async Task<Dictionary<string, string>> LoadRoleCacheAsync(CancellationToken ct)
    {
        // NormalizedName is usually unique in AspNetRoles
        // We store: "ADMIN" -> "<roleId>"
        var roles = await _db.AspNetRoles
            .AsNoTracking()
            .Where(r => r.NormalizedName != null)
            .Select(r => new { r.Id, NormalizedName = r.NormalizedName! })
            .ToListAsync(ct);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in roles)
            dict[r.NormalizedName.ToUpperInvariant()] = r.Id;

        return dict;
    }

    private async Task EnsureUserRolesAsync(
        AspNetUser user,
        string rolesText,
        Dictionary<string, string> roleIdByNormalizedName,
        CancellationToken ct)
    {
        // For existing users loaded WITHOUT Include(u=>u.Roles),
        // ensure roles collection is loaded so we can avoid duplicate join insert.
        var entry = _db.Entry(user);
        if (entry.State != EntityState.Added && !entry.Collection(u => u.Roles).IsLoaded)
        {
            await entry.Collection(u => u.Roles).LoadAsync(ct);
        }

        var roleNames = rolesText
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var roleName in roleNames)
        {
            var normalized = roleName.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!roleIdByNormalizedName.TryGetValue(normalized, out var roleId))
            {
                // If you want to AUTO-CREATE missing roles, do it here.
                // Default: skip + warn.
                _logger.LogWarning("Role not found (skipped): {Role}", roleName);
                continue;
            }

            // Get tracked role instance (avoid "already tracked" conflicts)
            var tracked = _db.AspNetRoles.Local.FirstOrDefault(r => r.Id == roleId);
            AspNetRole roleEntity;

            if (tracked != null)
            {
                roleEntity = tracked;
            }
            else
            {
                // Attach stub (we only need Id for join table)
                roleEntity = new AspNetRole { Id = roleId };
                _db.Attach(roleEntity);
            }

            // Avoid duplicates
            if (!user.Roles.Any(r => r.Id == roleId))
                user.Roles.Add(roleEntity);
        }
    }

    private static async IAsyncEnumerable<AspNetUserCsvRow> ReadRowsAsync(
        CsvReader csv,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var r in csv.GetRecords<AspNetUserCsvRow>())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Yield();
        }
    }
}
