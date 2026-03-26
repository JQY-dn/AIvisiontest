using AIvisiontest.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIvisiontest.Core.Database.Configurations
{
    // ── Lot 配置 ─────────────────────────────────────────────────────────────────

    public class LotConfiguration : IEntityTypeConfiguration<Lot>
    {
        public void Configure(EntityTypeBuilder<Lot> builder)
        {
            builder.ToTable("Lots");
            builder.HasKey(l => l.Id);

            builder.Property(l => l.LotNumber)
                .IsRequired()
                .HasMaxLength(50);

            // 批次号唯一索引
            builder.HasIndex(l => l.LotNumber)
                .IsUnique()
                .HasDatabaseName("IX_Lots_LotNumber");

            builder.Property(l => l.CreatedAt)
                .HasDefaultValueSql("GETDATE()");
        }
    }

    // ── Equipment 配置 ────────────────────────────────────────────────────────────

    public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
    {
        public void Configure(EntityTypeBuilder<Equipment> builder)
        {
            builder.ToTable("Equipments");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.EquipmentCode)
                .IsRequired()
                .HasMaxLength(50);

            // 设备编号唯一索引
            builder.HasIndex(e => e.EquipmentCode)
                .IsUnique()
                .HasDatabaseName("IX_Equipments_EquipmentCode");

            builder.Property(e => e.Status)
                .HasDefaultValue("Online");
        }
    }

    // ── WaferInspectionResult 配置 ────────────────────────────────────────────────

    public class WaferInspectionResultConfiguration : IEntityTypeConfiguration<WaferInspectionResult>
    {
        public void Configure(EntityTypeBuilder<WaferInspectionResult> builder)
        {
            builder.ToTable("WaferInspectionResults");
            builder.HasKey(w => w.Id);

            builder.Property(w => w.WaferId)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(w => w.DefectCount)
                .HasDefaultValue(0);

            builder.Property(w => w.InspectedAt)
                .HasDefaultValueSql("GETDATE()");

            builder.Property(w => w.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // 关联批次（多对一，批次删除时限制删除）
            builder.HasOne(w => w.Lot)
                .WithMany(l => l.InspectionResults)
                .HasForeignKey(w => w.LotId)
                .OnDelete(DeleteBehavior.Restrict);

            // 关联设备（多对一，设备删除时限制删除）
            builder.HasOne(w => w.Equipment)
                .WithMany(e => e.InspectionResults)
                .HasForeignKey(w => w.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 常用查询索引
            builder.HasIndex(w => w.LotId)
                .HasDatabaseName("IX_WaferInspectionResults_LotId");

            builder.HasIndex(w => w.EquipmentId)
                .HasDatabaseName("IX_WaferInspectionResults_EquipmentId");

            builder.HasIndex(w => w.InspectedAt)
                .HasDatabaseName("IX_WaferInspectionResults_InspectedAt");

            builder.HasIndex(w => w.IsPassed)
                .HasDatabaseName("IX_WaferInspectionResults_IsPassed");
        }
    }
}
