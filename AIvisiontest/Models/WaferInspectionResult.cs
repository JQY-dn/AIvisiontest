using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIvisiontest.Models
{
    /// <summary>
    /// 晶圆检测结果表：记录单枚晶圆的整体检测结果
    /// </summary>
    [Table("WaferInspectionResults")]
    public class WaferInspectionResult
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // ── 晶圆信息 ─────────────────────────────────────────────────────────

        /// <summary>晶圆编号（如 W001、Wafer-01）</summary>
        [Required]
        [MaxLength(50)]
        public string WaferId { get; set; } = "";

        /// <summary>晶圆在批次中的槽位序号（1~25）</summary>
        public int? SlotNumber { get; set; }

        // ── 关联批次 ─────────────────────────────────────────────────────────

        /// <summary>所属批次 ID（外键）</summary>
        [Required]
        public int LotId { get; set; }

        [ForeignKey(nameof(LotId))]
        public Lot? Lot { get; set; }

        // ── 关联设备 ─────────────────────────────────────────────────────────

        /// <summary>检测设备 ID（外键）</summary>
        [Required]
        public int EquipmentId { get; set; }

        [ForeignKey(nameof(EquipmentId))]
        public Equipment? Equipment { get; set; }

        // ── 检测过程 ─────────────────────────────────────────────────────────

        /// <summary>检测开始时间</summary>
        [Required]
        public DateTime InspectedAt { get; set; } = DateTime.Now;

        /// <summary>检测耗时（秒）</summary>
        public double? DurationSeconds { get; set; }

        /// <summary>检测员 / 操作员工号或姓名</summary>
        [MaxLength(50)]
        public string? Operator { get; set; }

        // ── 检测结果 ─────────────────────────────────────────────────────────

        /// <summary>整体检测结果：true = Pass，false = Fail</summary>
        [Required]
        public bool IsPassed { get; set; }

        /// <summary>缺陷总数量</summary>
        public int DefectCount { get; set; } = 0;

        /// <summary>
        /// 缺陷类型（多种类型用逗号分隔，如 "Scratch,Particle,Void"）。
        /// 如后续需要精细统计，可拆分为明细表。
        /// </summary>
        [MaxLength(200)]
        public string? DefectTypes { get; set; }

        /// <summary>缺陷密度（个/cm²），可由上层计算后写入</summary>
        public double? DefectDensity { get; set; }

        // ── 其他 ─────────────────────────────────────────────────────────────

        /// <summary>备注（异常说明、人工复判记录等）</summary>
        [MaxLength(1000)]
        public string? Remark { get; set; }

        /// <summary>记录创建时间（由系统自动写入）</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
