using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIvisiontest.Models
{
    /// <summary>
    /// 检测设备表：记录检测设备信息
    /// </summary>
    [Table("Equipments")]
    public class Equipment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>设备编号（唯一）</summary>
        [Required]
        [MaxLength(50)]
        public string EquipmentCode { get; set; } = "";

        /// <summary>设备名称</summary>
        [Required]
        [MaxLength(100)]
        public string EquipmentName { get; set; } = "";

        /// <summary>设备型号</summary>
        [MaxLength(100)]
        public string? Model { get; set; }

        /// <summary>设备状态（Online / Offline / Maintenance）</summary>
        [MaxLength(20)]
        public string Status { get; set; } = "Online";

        /// <summary>最后维护时间</summary>
        public DateTime? LastMaintenanceAt { get; set; }

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // 导航属性
        public ICollection<WaferInspectionResult> InspectionResults { get; set; } = new List<WaferInspectionResult>();
    }
}
