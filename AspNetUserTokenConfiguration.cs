using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CsvImport.ConsoleApp;

public sealed class AspNetUserTokenConfiguration : IEntityTypeConfiguration<AspNetUserToken>
{
    public void Configure(EntityTypeBuilder<AspNetUserToken> builder)
    {
        builder.ToTable("AspNetUserTokens"); // adjust if your table name differs

        builder.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.LoginProvider).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Value);

        builder.HasOne(d => d.User)
            .WithMany(p => p.AspNetUserTokens)
            .HasForeignKey(d => d.UserId);
    }
}
