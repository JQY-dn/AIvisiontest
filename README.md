# 🔬 AI工业视觉检测系统

<div align="center">

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?style=for-the-badge&logo=windows)
![YOLOv8](https://img.shields.io/badge/YOLOv8-Ultralytics-FF6B35?style=for-the-badge)
![ONNX](https://img.shields.io/badge/ONNX-Runtime-005CED?style=for-the-badge&logo=onnx)
![Python](https://img.shields.io/badge/Python-3.10+-3776AB?style=for-the-badge&logo=python)
![CUDA](https://img.shields.io/badge/CUDA-12.1-76B900?style=for-the-badge&logo=nvidia)

基于 YOLOv8 + ONNX Runtime 的工业视觉检测桌面应用，支持晶圆缺陷检测与汽车零部件入库检测（物料检测缺少数据集）。

[功能特性](#-功能特性) · [快速开始](#-快速开始) · [系统架构](#-系统架构) · [模型训练](#-模型训练) · [部署说明](#-部署说明)

</div>

---

## 📸 界面预览

| 晶圆缺陷检测 | 入库物料检测 |
|:---:|:---:|
| ![wafer](docs/wafer_demo.png) | ![parts](docs/parts_demo.png) |

---

## ✨ 功能特性

### 🔬 晶圆缺陷检测
- 支持 WM-811K 数据集，识别 **8 种缺陷模式**
- 实时推理，GPU 加速，单张 < 30ms
- 检测框颜色按缺陷类别区分，置信度可视化
- 支持拖拽图像 / 批量检测 / 导出标注结果图

### 📦 汽车零部件入库检测
- **双模型并行推理**：物料识别 + 缺陷检测同步执行
- 自动计数，支持期望数量校验（少件自动报警）
- 检测 4 类缺陷：划痕裂纹 / 变形弯曲 / 缺料少件 / 污染氧化
- 支持传送带相机实时接入，500ms 检测节拍
- 历史记录保留，一键导出 CSV 入库报告

### 🖥️ 系统通用
- 深色工业风 UI，Prism MVVM 架构
- GPU / CPU 自动切换，无 NVIDIA 显卡自动回退
- 拖拽 `.onnx` 文件直接加载模型
- 完整日志系统，支持数据库持久化

---

## 🚀 快速开始

### 环境要求

| 组件 | 版本 |
|------|------|
| Windows | 10 / 11 x64 |
| .NET SDK | 8.0+ |
| Visual Studio | 2022 17.8+ |
| Python | 3.10+（训练用）|
| CUDA（可选）| 12.1 + cuDNN 8.x |

### 1. 克隆项目

```bash
git clone https://github.com/your-username/ai-vision-inspection.git
cd ai-vision-inspection
```

### 2. 安装 Python 训练依赖

```bash
pip install ultralytics torch torchvision --index-url https://download.pytorch.org/whl/cu121
pip install onnx onnxruntime-gpu
```

### 3. 训练模型

**晶圆缺陷检测模型：**
```bash
# 预处理数据集
python wafer_yolo/data_processing/preprocess.py \
    --npz Wafer_Map_Datasets.npz \
    --output dataset

# 训练
set KMP_DUPLICATE_LIB_OK=TRUE
python wafer_yolo/train/train.py \
    --data dataset/dataset.yaml \
    --model yolov8s.pt \
    --epochs 100 --batch 16 --device 0
```

**零部件检测模型：**
```bash
python train_parts_defect.py \
    --task both \
    --part-names "bracket,bolt,gasket,shaft" \
    --epochs 150 --batch 16 --device 0
```

### 4. 导出 ONNX

```bash
python export_to_onnx.py \
    --weights runs/wafer_detect/<exp>/weights/best.pt \
    --imgsz 224
```

### 5. 运行 WPF 应用

```bash
cd AIvisiontest
dotnet restore
dotnet run
```

或在 Visual Studio 2022 中直接按 **F5**。

---

## 🏗️ 系统架构

```
AIvisiontest/
├── Models/
│   ├── DetectionResult.cs        # 检测结果数据模型
│   └── PartsInspectionModels.cs  # 零部件检测模型
├── Services/
│   ├── YoloInferenceService.cs   # ONNX 推理引擎（核心）
│   └── PartsInspectionService.cs # 双模型并行推理服务
├── ViewModels/
│   └── MainWindowViewModel.cs    # Prism MVVM ViewModel
├── Views/
│   ├── MainWindow.xaml           # 主界面
│   └── InboundInspectionView.xaml# 入库检测界面
└── wafer_yolo/                   # Python 训练脚本
    ├── data_processing/
    │   └── preprocess.py         # WM-811K 数据预处理
    ├── train/
    │   └── train.py              # YOLOv8 训练
    └── inference/
        └── predict.py            # Python 推理验证
```

### 推理流程

```
输入图像
  ↓ Letterbox Resize（保持宽高比填充）
  ↓ BGR → RGB → float32/255 → NCHW Tensor [1,3,H,W]
  ↓ ONNX Runtime（CUDA / CPU 自动选择）
  ↓ 输出解析 [1, nc+4, 8400]
  ↓ 置信度过滤 + 坐标反映射
  ↓ NMS（非极大值抑制）
  ↓ OpenCvSharp 绘制标注框
  ↓ WriteableBitmap → WPF Image 控件
```

---

## 🎯 模型性能

### 晶圆缺陷检测（WM-811K，224×224）

| 模型 | mAP50 | mAP50-95 | 推理速度（GPU）| 推理速度（CPU）|
|------|-------|----------|--------------|--------------|
| YOLOv8n | ~0.88 | ~0.76 | ~8ms | ~45ms |
| YOLOv8s | ~0.92 | ~0.81 | ~12ms | ~80ms |
| YOLOv8m | ~0.94 | ~0.84 | ~20ms | ~150ms |

### 支持的缺陷类别

| ID | 类别 | 描述 |
|----|------|------|
| 0 | Center | 晶圆中心区域缺陷 |
| 1 | Donut | 环形缺陷 |
| 2 | Edge-Loc | 边缘局部缺陷 |
| 3 | Edge-Ring | 边缘完整环形缺陷 |
| 4 | Loc | 局部随机缺陷 |
| 5 | Near-full | 近全面积缺陷 |
| 6 | Scratch | 线性划痕 |
| 7 | Random | 随机分布缺陷 |

---

## 📦 依赖说明

### C# / WPF

| 包 | 版本 | 用途 |
|----|------|------|
| Microsoft.ML.OnnxRuntime.Gpu | 1.18.0 | ONNX 推理引擎 |
| OpenCvSharp4.Windows | 4.9.0 | 图像处理与绘制 |
| Prism.DryIoc | 8.x | MVVM 框架 |
| CommunityToolkit.Mvvm | 8.2.2 | ViewModel 辅助 |

> 无 NVIDIA GPU？将 `OnnxRuntime.Gpu` 替换为 `OnnxRuntime` 即可使用 CPU 推理。

### Python（训练）

```bash
pip install ultralytics>=8.2.0
pip install torch>=2.0.0 torchvision
pip install onnx onnxruntime-gpu
pip install opencv-python numpy matplotlib tqdm
```

---

## ⚙️ 部署说明

### GPU 环境配置

1. 安装 [CUDA 12.1](https://developer.nvidia.com/cuda-12-1-0-download-archive)
2. 安装 [cuDNN 8.x](https://developer.nvidia.com/cudnn)
3. 验证：
```bash
python -c "import torch; print(torch.cuda.is_available())"
# 输出 True 即正常
```

### 常见问题

**Q: 启动报 `OMP: Error #15`**
```bash
# 在运行前设置环境变量
set KMP_DUPLICATE_LIB_OK=TRUE
```
或在代码入口最顶部添加：
```csharp
System.Environment.SetEnvironmentVariable("KMP_DUPLICATE_LIB_OK", "TRUE");
```

**Q: 模型加载后检测结果为空**
> 将置信度阈值调低至 0.15 后重试，或检查 ONNX 导出时的 `imgsz` 是否与代码中 `InputSize` 一致。

**Q: 无 GPU 时报 CUDA 错误**
> 取消勾选界面中「使用 GPU」选项，系统将自动切换至 CPU 推理。

---

## 📄 数据集

本项目晶圆检测部分使用 [WM-811K](https://www.kaggle.com/datasets/qingyi/wm811k-wafer-map) 公开数据集。

零部件检测数据集需自行采集标注，推荐工具：
- [LabelImg](https://github.com/HumanSignal/labelImg) — 轻量级，直接输出 YOLO 格式
- [Roboflow](https://roboflow.com) — 在线标注 + 自动增强 + 格式转换

---

## 📝 License

[MIT License](LICENSE) © 2025

---

<div align="center">
如果这个项目对你有帮助，欢迎 ⭐ Star 支持！
</div>
