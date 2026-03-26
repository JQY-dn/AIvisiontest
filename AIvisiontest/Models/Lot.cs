using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIvisiontest.Models
{
    /// <summary>
    /// 批次表：记录晶圆批次信息
    /// </summary>
    [Table("Lots")]
    public class Lot
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>批次号（唯一）</summary>
        [Required]
        [MaxLength(50)]
        public string LotNumber { get; set; } = "";

        /// <summary>产品型号</summary>
        [MaxLength(100)]
        public string? ProductModel { get; set; }

        /// <summary>批次创建时间</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // 导航属性
        public ICollection<WaferInspectionResult> InspectionResults { get; set; } = new List<WaferInspectionResult>();
    }
}
