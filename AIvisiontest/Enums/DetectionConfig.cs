using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIvisiontest.Enums
{
    /// <summary>
    /// 检测模型枚举（对应第一个ComboBox的Value）
    /// </summary>
    public enum DetectionModel
    {
        YOLOv8n_seg = 0,
        YOLOv8m_seg = 1,
        YOLOv8x_seg = 2,
        YOLOv9c = 3,
        RT_DETR_L = 4
    }

    /// <summary>
    /// 检测功能枚举（对应第二个ComboBox的Value）
    /// </summary>
    public enum DetectionFunction
    {
        MaterialAndDefect = 0,
        OnlyMaterial = 1,
        OnlyDefect = 2
    }
}
