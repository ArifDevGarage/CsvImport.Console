using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CsvImport.ConsoleApp;

public sealed class AspNetUserLoginConfiguration : IEntityTypeConfiguration<AspNetUserLogin>
{
    public void Configure(EntityTypeBuilder<AspNetUserLogin> builder)
    {
        // Identity default table name is usually AspNetUserLogins
        builder.ToTable("AspNetUserLogins");

        builder.HasKey(e => new { e.LoginProvider, e.ProviderKey });

        builder.Property(e => e.LoginProvider).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ProviderKey).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ProviderDisplayName).HasMaxLength(256); // optional (adjust if you want)
        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();

        builder.HasOne(e => e.User)
            .WithMany(u => u.AspNetUserLogins)
            .HasForeignKey(e => e.UserId);
    }
}
