using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CsvImport.ConsoleApp;

public sealed class AspNetUserClaimConfiguration : IEntityTypeConfiguration<AspNetUserClaim>
{
    public void Configure(EntityTypeBuilder<AspNetUserClaim> builder)
    {
        builder.ToTable("AspNetUserClaims");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .HasMaxLength(450)
            .IsRequired();

        // Identity default schema usually uses nvarchar(max) for these;
        // so DON'T set max length unless you're sure.
        builder.Property(x => x.ClaimType);
        builder.Property(x => x.ClaimValue);

        builder.HasOne(x => x.User)
            .WithMany(u => u.AspNetUserClaims)
            .HasForeignKey(x => x.UserId);
    }
}
