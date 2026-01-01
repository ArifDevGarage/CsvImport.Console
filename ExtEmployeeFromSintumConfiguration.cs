using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CsvImport.ConsoleApp;

public sealed class ExtEmployeeFromSintumConfiguration : IEntityTypeConfiguration<ExtEmployeeFromSintum>
{
    public void Configure(EntityTypeBuilder<ExtEmployeeFromSintum> entity)
    {
        entity.ToTable("ExtEmployeeFromSinta", "ExternalData"); // keep exactly as your DB

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Area)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.CostCenter)
            .HasMaxLength(10)
            .IsUnicode(false);

        entity.Property(e => e.Department)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.Directorate)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.Email)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.EmployeeId)
            .HasMaxLength(20)
            .IsUnicode(false);

        entity.Property(e => e.EmployeeName)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.Entity)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.Function)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.HelperEmployeePositionTypeId)
            .HasColumnName("Helper_EmployeePositionTypeId");

        entity.Property(e => e.HelperIsDelegate)
            .HasColumnName("Helper_IsDelegate");

        entity.Property(e => e.LastUpdate)
            .HasColumnType("datetime");

        entity.Property(e => e.Level)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.PlantArea)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.Posgrd)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.PositionId)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        entity.Property(e => e.PositionName)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.SuperiorId)
            .HasMaxLength(20)
            .IsUnicode(false);

        entity.Property(e => e.SuperiorPositionId)
            .HasMaxLength(100)
            .IsUnicode(false);

        entity.Property(e => e.Unit)
            .HasMaxLength(50)
            .IsUnicode(false);

        entity.Property(e => e.UserName)
            .HasMaxLength(30)
            .IsUnicode(false);

        // OPTIONAL (recommended) unique index if your DB rules allow it:
        // entity.HasIndex(e => new { e.EmployeeId, e.PositionId }).IsUnique();
    }
}