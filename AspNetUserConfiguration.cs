using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CsvImport.ConsoleApp;

public class AspNetUserConfiguration : IEntityTypeConfiguration<AspNetUser>
{
    public void Configure(EntityTypeBuilder<AspNetUser> builder)
    {

        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.NormalizedEmail).HasMaxLength(256);
        builder.Property(e => e.NormalizedUserName).HasMaxLength(256);
        builder.Property(e => e.UserName).HasMaxLength(256);

        builder.HasMany(d => d.Roles).WithMany(p => p.Users)
            .UsingEntity<Dictionary<string, object>>(
                "AspNetUserRole",
                r => r.HasOne<AspNetRole>().WithMany().HasForeignKey("RoleId"),
                l => l.HasOne<AspNetUser>().WithMany().HasForeignKey("UserId"),
                j =>
                {
                    j.HasKey("UserId", "RoleId");
                    j.ToTable("AspNetUserRoles");
                });
    }

    //     builder.ToTable("Customers");
    //     builder.HasKey(x => x.Id);

    //     builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
    //     builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    //     builder.Property(x => x.Email).HasMaxLength(200);

    //     builder.HasIndex(x => x.Code).IsUnique();
    // }


}
