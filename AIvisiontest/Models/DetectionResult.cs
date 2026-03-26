using OpenCvSharp;
using System.Collections.Generic;
using System.Windows.Media;

namespace AIvisiontest.Models
{
    public class DetectionBox
    {
        public string ClassName { get; set; } = "";
        public int ClassId { get; set; }
        public float Confidence { get; set; }
        public OpenCvSharp.Rect Box { get; set; }   // 原图像素坐标（int）

        public string Label => $"{ClassName}  {Confidence:P0}";
        public Brush Color => ClassColors.Get(ClassName);

        // WPF 绑定用（XAML 里显示坐标）
        public double BoxX => Box.X;
        public double BoxY => Box.Y;
        public double BoxWidth => Box.Width;
        public double BoxHeight => Box.Height;
    }

    public class DetectionResult
    {
        public List<DetectionBox> Boxes { get; set; } = new();
        public double InferenceMs { get; set; }
        public bool HasDefects => Boxes.Count > 0;
        public string Summary => HasDefects
            ? $"发现 {Boxes.Count} 处缺陷"
            : "未检测到缺陷";
    }

    public static class ClassColors
    {
        private static readonly Dictionary<string, Color> _map = new()
        {
            ["Center"] = Color.FromRgb(255, 80, 80),
            ["Donut"] = Color.FromRgb(80, 220, 80),
            ["Edge-Loc"] = Color.FromRgb(80, 130, 255),
            ["Edge-Ring"] = Color.FromRgb(255, 200, 0),
            ["Loc"] = Color.FromRgb(200, 50, 255),
            ["Near-full"] = Color.FromRgb(0, 210, 210),
            ["Scratch"] = Color.FromRgb(255, 140, 0),
            ["Random"] = Color.FromRgb(180, 180, 180),
        };

        public static Brush Get(string cls) =>
            new SolidColorBrush(_map.TryGetValue(cls, out var c) ? c : Colors.White);

        public static Color GetColor(string cls) =>
            _map.TryGetValue(cls, out var c) ? c : Colors.White;
    }
}
