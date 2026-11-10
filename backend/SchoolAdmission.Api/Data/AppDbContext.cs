using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Hold> Holds => Set<Hold>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<SchoolClass>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasMaxLength(64);
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.HasMany(c => c.Holds).WithOne(h => h.Class).HasForeignKey(h => h.ClassId);
        });

        model.Entity<Hold>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.Id).HasMaxLength(64);
            e.Property(h => h.ClassId).HasMaxLength(64);
            e.Property(h => h.StudentId).HasMaxLength(17).IsRequired();
            e.Property(h => h.Status)
                .HasConversion(v => v.ToString().ToUpperInvariant(),
                    v => Enum.Parse<HoldStatus>(v, true))
                .HasMaxLength(20);

            e.HasIndex(h => h.StudentId);

            // A student may have only one active application per class.
            e.HasIndex(h => new { h.ClassId, h.StudentId })
                .IsUnique()
                .HasFilter("\"Status\" IN ('HELD', 'CONFIRMED')");
        });
    }
}
