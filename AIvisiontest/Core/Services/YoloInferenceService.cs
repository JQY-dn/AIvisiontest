using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AIvisiontest.Models;
using Rect = OpenCvSharp.Rect;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace AIvisiontest.Core.Services
{
    public class YoloInferenceService : IDisposable
    {
        public static readonly string[] ClassNames =
            { "Center","Donut","Edge-Loc","Edge-Ring","Loc","Near-full","Scratch","Random" };

        private const int InputSize = 224;
        private InferenceSession? _session;
        private string? _inputName;
        private bool _disposed;

        public bool IsLoaded => _session != null;
        public string ModelPath { get; private set; } = "";

        public void Load(string onnxPath, bool useGpu = true)
        {
            _session?.Dispose();
            var opt = new SessionOptions();
            opt.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            if (useGpu)
            {
                try { opt.AppendExecutionProvider_CUDA(0); }
                catch { }
            }
            _session = new InferenceSession(onnxPath, opt);
            _inputName = _session.InputMetadata.Keys.First();
            ModelPath = onnxPath;
        }

        public DetectionResult Predict(string imagePath, float conf = 0.25f, float iou = 0.45f)
        {
            if (_session == null)
                throw new InvalidOperationException("请先调用 Load() 加载模型");
            using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (src.Empty())
                throw new FileNotFoundException($"无法读取图像: {imagePath}");
            return PredictMat(src, conf, iou);
        }

        public DetectionResult PredictMat(Mat srcBgr, float conf = 0.25f, float iou = 0.45f)
        {
            var sw = Stopwatch.StartNew();
            using var lb = Letterbox(srcBgr, out float sx, out float sy, out int padL, out int padT);
            var tensor = ToTensor(lb);
            var inputs = new[] { NamedOnnxValue.CreateFromTensor(_inputName!, tensor) };
            using var outs = _session!.Run(inputs);
            var raw = outs.First().AsTensor<float>();
            var boxes = Parse(raw, conf, sx, sy, padL, padT, srcBgr.Width, srcBgr.Height);
            var final = Nms(boxes, iou);
            sw.Stop();
            return new DetectionResult { Boxes = final, InferenceMs = sw.Elapsed.TotalMilliseconds };
        }

        public static Mat DrawBoxes(Mat src, DetectionResult result)
        {
            var vis = src.Clone();
            foreach (var b in result.Boxes)
            {
                var c = ClassColors.GetColor(b.ClassName);
                var col = new Scalar(c.B, c.G, c.R);
                Cv2.Rectangle(vis, b.Box, col, 2);
                string lbl = $"{b.ClassName} {b.Confidence:P0}";
                int baseline;
                var ts = Cv2.GetTextSize(lbl, HersheyFonts.HersheySimplex, 0.55, 1, out baseline);
                var bgR = new Rect(Math.Max(0, b.Box.X),
                                   Math.Max(0, b.Box.Y - ts.Height - 8),
                                   ts.Width + 6, ts.Height + baseline + 8);
                Cv2.Rectangle(vis, bgR, col, -1);
                Cv2.PutText(vis, lbl, new Point(b.Box.X + 3, b.Box.Y - 4),
                    HersheyFonts.HersheySimplex, 0.55, new Scalar(0, 0, 0), 1);

                
            }
            string summary = result.HasDefects ? $"Defects: {result.Boxes.Count}" : "Normal";
            Cv2.PutText(vis, summary, new Point(10, 30), HersheyFonts.HersheySimplex, 0.9,
                result.HasDefects ? new Scalar(0, 50, 255) : new Scalar(0, 200, 0), 2);
            return vis;
        }

        private static Mat Letterbox(Mat src, out float sx, out float sy, out int padL, out int padT)
        {
            float r = Math.Min((float)InputSize / src.Width, (float)InputSize / src.Height);
            int nw = (int)Math.Round(src.Width * r);
            int nh = (int)Math.Round(src.Height * r);
            padL = (InputSize - nw) / 2;
            padT = (InputSize - nh) / 2;
            sx = sy = r;
            using var resized = new Mat();
            Cv2.Resize(src, resized, new Size(nw, nh));
            var canvas = new Mat(new Size(InputSize, InputSize), MatType.CV_8UC3, new Scalar(114, 114, 114));
            resized.CopyTo(canvas[new Rect(padL, padT, nw, nh)]);
            return canvas;
        }

        private static DenseTensor<float> ToTensor(Mat bgr)
        {
            using var rgb = new Mat();
            Cv2.CvtColor(bgr, rgb, ColorConversionCodes.BGR2RGB);
            int h = rgb.Height, w = rgb.Width;
            var t = new DenseTensor<float>(new[] { 1, 3, h, w });
            for (int c = 0; c < 3; c++)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        t[0, c, y, x] = rgb.At<Vec3b>(y, x)[c] / 255f;
            return t;
        }

        private static List<DetectionBox> Parse(Tensor<float> raw,
            float conf, float sx, float sy, int padL, int padT, int origW, int origH)
        {
            int nc = ClassNames.Length, nPred = raw.Dimensions[2];
            var list = new List<DetectionBox>();
            for (int i = 0; i < nPred; i++)
            {
                float best = 0; int cls = 0;
                for (int c = 0; c < nc; c++)
                {
                    float s = raw[0, 4 + c, i];
                    if (s > best) { best = s; cls = c; }
                }



                if (best < conf) continue;
                float cx = raw[0, 0, i], cy = raw[0, 1, i];
                float bw = raw[0, 2, i], bh = raw[0, 3, i];

                Debug.WriteLine($"Raw box [{i}]: cx={cx:F1} cy={cy:F1} w={bw:F1} h={bh:F1} conf={best:F2} cls={ClassNames[cls]}");
                float x1 = Math.Clamp(((cx - bw / 2) - padL) / sx, 0, origW);
                float y1 = Math.Clamp(((cy - bh / 2) - padT) / sy, 0, origH);
                float x2 = Math.Clamp(((cx + bw / 2) - padL) / sx, 0, origW);
                float y2 = Math.Clamp(((cy + bh / 2) - padT) / sy, 0, origH);
                if (x2 <= x1 || y2 <= y1) continue;

                // ✅ float → int，与 OpenCvSharp.Rect 完全匹配
                
                
                list.Add(new DetectionBox
                {
                    ClassId = cls,
                    ClassName = ClassNames[cls],
                    Confidence = best,
                    Box = new Rect((int)x1, (int)y1, (int)(x2 - x1), (int)(y2 - y1)),
                });
            }
            return list;
        }

        private static List<DetectionBox> Nms(List<DetectionBox> boxes, float iouThr)
        {
            var sorted = boxes.OrderByDescending(b => b.Confidence).ToList();
            var kept = new List<DetectionBox>();
            var removed = new HashSet<int>();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (removed.Contains(i)) continue;
                kept.Add(sorted[i]);
                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (removed.Contains(j)) continue;
                    if (Iou(sorted[i].Box, sorted[j].Box) > iouThr)
                        removed.Add(j);
                }
            }
            return kept;
        }

        // ✅ 全用 OpenCvSharp.Rect，Right/Bottom 均为 int
        private static float Iou(Rect a, Rect b)
        {
            int ix = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X));
            int iy = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y));
            double inter = (double)ix * iy;
            if (inter <= 0) return 0f;
            return (float)(inter / ((double)a.Width * a.Height + (double)b.Width * b.Height - inter));
        }

        public void Dispose()
        {
            if (!_disposed) { _session?.Dispose(); _disposed = true; }
        }
    }
}
