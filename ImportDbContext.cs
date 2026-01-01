using Microsoft.EntityFrameworkCore;

namespace CsvImport.ConsoleApp;

public sealed class ImportDbContext : DbContext
{
    public ImportDbContext(DbContextOptions<ImportDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AspNetUser> AspNetUsers => Set<AspNetUser>();
    public DbSet<AspNetRole> AspNetRoles => Set<AspNetRole>();
    public DbSet<AspNetRoleClaim> AspNetRoleClaims => Set<AspNetRoleClaim>();
    public DbSet<AspNetUserClaim> AspNetUserClaims => Set<AspNetUserClaim>();
    public DbSet<AspNetUserLogin> AspNetUserLogins => Set<AspNetUserLogin>();
    public DbSet<AspNetUserToken> AspNetUserTokens => Set<AspNetUserToken>();
    public DbSet<ExtEmployeeFromSintum> ExtEmployeeFromSintums => Set<ExtEmployeeFromSintum>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // This automatically finds all classes implementing IEntityTypeConfiguration in the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ImportDbContext).Assembly);
    }
}