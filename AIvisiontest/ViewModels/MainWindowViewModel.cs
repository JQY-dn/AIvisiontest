using AIvisiontest.Core.Database;
using AIvisiontest.Core.Logging;
using AIvisiontest.Core.Services;
using AIvisiontest.Models;
using AIvisiontest.Tools;
using Microsoft.Win32;
using OpenCvSharp;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static OpenCvSharp.FileStorage;
using IOPath = System.IO.Path;

namespace AIvisiontest.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private string _title = "Prism Application";
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private readonly ILogService _log;
        private readonly YoloInferenceService _yolo = new();
        private readonly IDatabaseService _db;


        public ObservableCollection<DetectionBox> Boxes { get; } = new();


        /// <summary>绑定到 ListBox 的日志列表</summary>
        public ObservableCollection<LogEntry> Logs { get; } = new();

        /// <summary>UI 最多显示的日志条数，超出后自动移除最旧的</summary>
        private const int MaxLogCount = 200;


        // ── 绑定属性 ──────────────────────────────────────────────

        private string _modelPath = "未加载模型";

        private bool _useGpu = true;
        private float _confThreshold = 0.25f;
        private float _iouThreshold = 0.45f;
        private bool _isBusy = false;
        private string _statusText = "请加载 ONNX 模型";
        private string _inferenceInfo = "";
        private int TotalChecked = 0;
        private int DefectCount = 0;
        private double AvgMs = 0;


        private BitmapSource? _resultImage;

        private bool _isModelLoaded;


        public Brush ModelStatusColor => _isModelLoaded
                ? new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88))  // #FF00FF88
                : new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)); // 灰色


        // ─── 检测状态 ──────────────────────────────────────────────
        private bool _isDetecting;
        public bool IsDetecting
        {
            get => _isDetecting;
            set
            {
                SetProperty(ref _isDetecting, value);
                RaisePropertyChanged(nameof(StartStopText));
            }
        }
        public string StartStopText => _isDetecting ? "⏹ 停止检测" : "▶ 开始检测";

        // ─── 模式状态 ──────────────────────────────────────────────
        private bool _isRealTimeMode = true;
        public bool IsRealTimeMode
        {
            get => _isRealTimeMode;
            set
            {
                SetProperty(ref _isRealTimeMode, value);
                RaisePropertyChanged(nameof(IsManualMode));
            }
        }
        public bool IsManualMode => !_isRealTimeMode;

        //----------------------按钮----------------------------------------------------------------------------------

        //开始检测
        public DelegateCommand ToggleDetectionCommand { get; }

        //模式选择
        public DelegateCommand SwitchToRealTimeCommand { get; }//实时
        public DelegateCommand SwitchToManualCommand { get; }//手动

        //暂停检测
        public DelegateCommand PauseDetectionCommand { get; }

        //停止检测
        public DelegateCommand StopDetectionCommand { get; }

        //前一个图像与下一个图像
        public DelegateCommand PrevImageCommand {  get; }
        public DelegateCommand NextImageCommand { get; } 
        //加载模型
        public DelegateCommand LoadModelCommand { get; }
        //退出程序
        public DelegateCommand Exit { get; }
        //最小化窗口
        public DelegateCommand MinButton { get; }
        //最大化窗口
        public DelegateCommand MaxButton { get; }

        // 定义可绑定的图片源属性
        private BitmapSource _backgroundImageSource;
        public BitmapSource BackgroundImageSource
        {
            get => _backgroundImageSource;
            set => SetProperty(ref _backgroundImageSource, value); // Prism 的 SetProperty 实现通知
        }

        

        public MainWindowViewModel()
        {
            
            ToggleDetectionCommand = new DelegateCommand(async()=> await OnToggleDetection());
            Exit = new DelegateCommand(onExit);
            MinButton = new DelegateCommand(onMinButton);
            MaxButton = new DelegateCommand(onMaxButton);
            LoadModelCommand = new DelegateCommand(async () => await LoadModelButtonAsync());

            SwitchToRealTimeCommand = new DelegateCommand(() => IsRealTimeMode = true);
            SwitchToManualCommand = new DelegateCommand(() => IsRealTimeMode = false);
            
        }
        public MainWindowViewModel(IDatabaseService db,ILogService log) : this()
        {
            _log = log;
            _db = db;

            // 订阅日志事件
            _log.OnLogEntry += OnLogReceived;

            _log.Info("测试数据初始化完成", tag: nameof(MainWindowViewModel));

            _ = InitTestDataAsync(); // 启动时确保测试数据存在

        }

        /// <summary>
        /// 收到新日志时的回调。
        /// LogService 在业务线程触发，必须切回 UI 线程才能更新 ObservableCollection。
        /// </summary>
        private void OnLogReceived(LogEntry entry)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Logs.Add(entry);

                // 超出上限时移除最旧的一条
                if (Logs.Count > MaxLogCount)
                    Logs.RemoveAt(0);
            });
        }

        private async Task InitTestDataAsync()
        {
            try
            {
                // 插入测试批次（如果不存在）
                var lotExists = await _db.AnyAsync<Lot>(l => l.LotNumber == "LOT-001");
                if (!lotExists)
                {
                    await _db.InsertAsync(new Lot
                    {
                        LotNumber = "LOT-001",
                        ProductModel = "TestProduct",
                        CreatedAt = DateTime.Now
                    });
                }

                // 插入测试设备（如果不存在）
                var equipExists = await _db.AnyAsync<Equipment>(e => e.EquipmentCode == "EQ-001");
                if (!equipExists)
                {
                    await _db.InsertAsync(new Equipment
                    {
                        EquipmentCode = "EQ-001",
                        EquipmentName = "测试设备1",
                        Status = "Online"
                    });
                }

                _log.Info("测试数据初始化完成", tag: nameof(MainWindowViewModel));
            }
            catch (Exception ex)
            {
                _log.Error("测试数据初始化失败", ex, tag: nameof(MainWindowViewModel));
            }
        }


        // ═══════════════════════════════════════════════════════
        // 加载模型
        // ═══════════════════════════════════════════════════════
        
        private async Task LoadModelAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择晶圆缺陷检测 ONNX 模型",
                Filter = "ONNX 模型|*.onnx",
            };
            if (dlg.ShowDialog() != true) return;

            _isBusy = true;
            _statusText = "正在加载模型...";
            _log.Info(_statusText+"  " + IOPath.GetFileName(_modelPath), tag: nameof(MainWindowViewModel));

            var loadingToast = ToastService.Show(ToastType.Loading,_statusText,IOPath.GetFileName(_modelPath));
            try
            {
                string path = dlg.FileName;
                await Task.Run(() => _yolo.Load(path, _useGpu));
                _modelPath = IOPath.GetFileName(path);
                _statusText = $"✅ 模型已就绪：{_modelPath}";
                ToastService.Show(ToastType.Success, _statusText, IOPath.GetFileName(_modelPath));
                _log.Info(_statusText+"  " + IOPath.GetFileName(_modelPath), tag: nameof(MainWindowViewModel));
                _isModelLoaded = true;                  // ← 加这行
                RaisePropertyChanged(nameof(ModelStatusColor)); // ← 通知UI
                //RaisePropertyChanged(nameof(ModelStatusText));  // ← 通知UI

            }
            catch (Exception ex)
            {
                _statusText = $"❌ 加载失败：{ex.Message}";
                ToastService.Show(ToastType.Error, _statusText, IOPath.GetFileName(_modelPath));
                _log.Error(_statusText+"  " + IOPath.GetFileName(_modelPath), ex, tag: nameof(MainWindowViewModel));
                _isModelLoaded = false;                 // ← 加这行
                RaisePropertyChanged(nameof(ModelStatusColor));
                //RaisePropertyChanged(nameof(ModelStatusText));
            }
            finally { 
                
                loadingToast.Dismiss();
                _isBusy = false; 
            }
        }
        //触发模型加载按钮
        private async Task LoadModelButtonAsync()
        {
            await LoadModelAsync();
        }


        //开始检测逻辑
        private async Task OnToggleDetection()
        {
            if (!_isModelLoaded)
            {
                ToastService.Show(ToastType.Error, "请先加载模型");
                return;
            }

            string path = "";

            if (IsRealTimeMode)
            {
                _log.Info("实时检测模式已选择", tag: nameof(MainWindowViewModel));
                var dlg = new OpenFileDialog
                {
                    Title = "选择晶圆图像",
                    Filter = "图像文件|*.jpg;*.jpeg;*.png;*.bmp",
                    Multiselect = true,
                };
                if (dlg.ShowDialog() != true) return;

                foreach (var file in dlg.FileNames)
                    await RunAsync(file);


                


            }


            SaveImage();
            

            
            
           
            


        }


        // ═══════════════════════════════════════════════════════
        // 核心检测流程
        // ═══════════════════════════════════════════════════════
        private async Task RunAsync(string imagePath)
        {
            _isBusy = true;
            _statusText = $"检测中：{IOPath.GetFileName(imagePath)}";
            Boxes.Clear();

            try
            {
                var result = await Task.Run(() =>
                    _yolo.Predict(imagePath, _confThreshold, _iouThreshold));

                // 更新结果列表
                foreach (var b in result.Boxes)
                    Boxes.Add(b);

                // 生成标注图
                _resultImage = await Task.Run(() =>
                {
                    using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
                    using var vis = YoloInferenceService.DrawBoxes(src, result);
                    
                    return MatToBitmap(vis);
                });


                BackgroundImageSource = _resultImage;
                
                // 状态 & 统计
                _inferenceInfo = $"耗时 {result.InferenceMs:F0} ms  |  " +
                                $"分辨率 {GetImageSize(imagePath)}  |  " +
                                $"缺陷数 {result.Boxes.Count}";

                _statusText = result.HasDefects
                    ? $"⚠️ 发现 {result.Boxes.Count} 处缺陷"
                    : "✅ 未检测到缺陷，晶圆正常";

                ToastService.Show(ToastType.Success, _inferenceInfo,_statusText);

                TotalChecked++;
                if (result.HasDefects) DefectCount++;
                AvgMs = (AvgMs * (TotalChecked - 1) + result.InferenceMs) / TotalChecked;
                
                _log.Info("检测完成："+_statusText+"   "+_inferenceInfo, tag: nameof(MainWindowViewModel));
            }
            catch (Exception ex)
            {
                _statusText = $"❌ {ex.Message}";
            }
            finally { _isBusy = false; }
        }

        // ═══════════════════════════════════════════════════════
        // 保存结果图
        // ═══════════════════════════════════════════════════════
        
        private void SaveImage()
        {
            if (_resultImage == null) return;
            var dlg = new SaveFileDialog
            {
                Title = "保存标注结果图",
                Filter = "PNG 图像|*.png",
                FileName = $"wafer_result_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            };
            if (dlg.ShowDialog() != true) return;
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(_resultImage));
            using var fs = File.OpenWrite(dlg.FileName);
            enc.Save(fs);
            _statusText = $"✅ 已保存：{IOPath.GetFileName(dlg.FileName)}";
        }

        // ── 工具方法 ──────────────────────────────────────────────
        private static BitmapSource MatToBitmap(Mat mat)
        {
            using var rgba = new Mat();
            Cv2.CvtColor(mat, rgba, ColorConversionCodes.BGR2BGRA);

            var bmp = new WriteableBitmap(
                rgba.Width, rgba.Height, 96, 96,
                System.Windows.Media.PixelFormats.Bgra32, null);

            bmp.Lock();
            int stride = rgba.Width * 4;
            byte[] buffer = new byte[stride * rgba.Height];
            System.Runtime.InteropServices.Marshal.Copy(
                rgba.Data, buffer, 0, buffer.Length);
            System.Runtime.InteropServices.Marshal.Copy(
                buffer, 0, bmp.BackBuffer, buffer.Length);
            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();
            bmp.Freeze();
            return bmp;
        }

        private static string GetImageSize(string path)
        {
            try
            {
                using var m = Cv2.ImRead(path, ImreadModes.Color);
                return $"{m.Width}×{m.Height}";
            }
            catch { return "未知"; }
        }












        //退出
        private void onExit()
        {
            Application.Current.Shutdown();
        }
        //最小化
        private void onMinButton()
        {
            Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }
        //最大
        private void onMaxButton()
        {
            if (Application.Current.MainWindow.WindowState == WindowState.Maximized)
            {
                Application.Current.MainWindow.WindowState = WindowState.Normal;
            }
            else
            {
                Application.Current.MainWindow.WindowState = WindowState.Maximized;
            }
        }


        // 记得在 Dispose / 页面卸载时取消订阅，防止内存泄漏
        public void Dispose()
        {
            _log.OnLogEntry -= OnLogReceived;
        }
    }
}
