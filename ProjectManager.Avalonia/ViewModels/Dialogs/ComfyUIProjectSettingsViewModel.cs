using Avalonia.Platform.Storage;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Collections.ObjectModel;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class ComfyUIProjectSettingsViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly ILanguageService _languageService;
    private Project? _project;
    private bool _isUpdatingCommand = false;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _projectPath = string.Empty;

    /// <summary>
    /// ComfyUI 根目录（包含 main.py 与 custom_nodes 的目录）。
    /// </summary>
    [ObservableProperty]
    private string _comfyUIRootPath = string.Empty;

    partial void OnComfyUIRootPathChanged(string value)
    {
        // 根目录改变会影响 main.py 的实际路径，自动刷新启动命令
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    [ObservableProperty]
    private string _startCommand = "python main.py";

    [ObservableProperty]
    private string _runCommand = "python main.py";

    [ObservableProperty]
    private string _commandLineArguments = string.Empty;

    partial void OnRunCommandChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateCompleteStartCommand();
        }
    }

    partial void OnCommandLineArgumentsChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateCompleteStartCommand();
        }
    }

    /// <summary>
    /// 更新完整的启动命令（运行命令 + 命令行参数）
    /// </summary>
    private void UpdateCompleteStartCommand()
    {
        _isUpdatingCommand = true;
        try
        {
            var runCmd = string.IsNullOrWhiteSpace(RunCommand) ? "python main.py" : RunCommand.Trim();
            var args = string.IsNullOrWhiteSpace(CommandLineArguments) ? "" : CommandLineArguments.Trim();

            StartCommand = string.IsNullOrWhiteSpace(args) ? runCmd : $"{runCmd} {args}";
            OnPropertyChanged(nameof(StartCommand));
        }
        finally
        {
            _isUpdatingCommand = false;
        }
    }

    [ObservableProperty]
    private int? _port = 8188;

    partial void OnPortChanged(int? value)
    {
        // 端口变化时立即更新启动命令
        if (!_isUpdatingCommand)
        {
            // 如果端口为空，设置为默认值8188
            if (!value.HasValue)
            {
                _port = 8188;
                OnPropertyChanged(nameof(Port));
                UpdateStartCommand();
                return;
            }

            var portValue = value.Value;

            // 验证端口号：必须在1-65535范围内，建议使用1024-65535
            if (portValue < 1)
            {
                Port = 8188; // 恢复默认值
                return;
            }
            if (portValue > 65535)
            {
                Port = 65535; // 限制最大端口号
                return;
            }
            if (portValue < 1024)
            {
                // 端口号小于1024需要管理员权限，显示警告但允许设置
                // 可以考虑在UI中显示警告信息
            }
            UpdateStartCommand();
        }
    }

    partial void OnStartCommandChanged(string value)
    {
        // 启动命令手动修改时，解析并更新对应的设置选项
        if (!_isUpdatingCommand)
        {
            ParseStartCommand(value);
            UpdateStartCommandSuggestions();
        }
    }

    [ObservableProperty]
    private string _pythonPath = string.Empty;

    [ObservableProperty]
    private bool _isPythonPathValid = true;

    [ObservableProperty]
    private ObservableCollection<string> _startCommandSuggestions = new ObservableCollection<string>();

    partial void OnPythonPathChanged(string value)
    {
        // 验证Python路径有效性
        var isValid = IsValidPythonPath(value);
        IsPythonPathValid = isValid;

        // Python路径变化时验证有效性，只有有效路径才更新启动命令
        if (!_isUpdatingCommand && isValid)
        {
            UpdateStartCommand();
        }

        // 如果路径无效，仍然需要更新建议（使用默认python）
        if (!_isUpdatingCommand && !isValid)
        {
            UpdateStartCommandSuggestions();
        }
    }

    private bool IsValidPythonPath(string path)
    {
        // 如果路径为空，认为是有效的（使用默认python命令）
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        try
        {
            // 检查路径格式是否有效
            var fullPath = Path.GetFullPath(path);

            // 检查文件是否存在
            if (!File.Exists(fullPath))
            {
                return false;
            }

            // 检查文件名是否为python.exe（不区分大小写）
            var fileName = Path.GetFileName(fullPath);
            if (!fileName.Equals("python.exe", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            // 如果路径格式无效，返回false
            return false;
        }
    }

    [ObservableProperty]
    private bool _listenAllInterfaces = false;

    partial void OnListenAllInterfacesChanged(bool value)
    {
        // 监听模式变化时立即更新启动命令
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    // === 枚举属性变更处理 ===
    partial void OnMemoryManagementModeChanged(MemoryManagementMode value)
    {
        if (!_isUpdatingCommand)
        {
            // 同步更新对应的布尔属性（保持向后兼容）
            GpuOnly = value == MemoryManagementMode.GpuOnly;
            HighVram = value == MemoryManagementMode.HighVram;
            NormalVram = value == MemoryManagementMode.NormalVram;
            LowVramMode = value == MemoryManagementMode.LowVram;
            NoVram = value == MemoryManagementMode.NoVram;
            CpuMode = value == MemoryManagementMode.CpuMode;

            // 触发命令更新
            UpdateStartCommand();
        }
    }

    partial void OnUnetPrecisionModeChanged(UNetPrecisionMode value)
    {
        if (!_isUpdatingCommand)
        {
            // 同步更新对应的布尔属性（保持向后兼容）
            Fp32Unet = value == UNetPrecisionMode.FP32;
            Fp64Unet = value == UNetPrecisionMode.FP64;
            Bf16Unet = value == UNetPrecisionMode.BF16;
            Fp16Unet = value == UNetPrecisionMode.FP16;
            Fp8E4M3FnUnet = value == UNetPrecisionMode.FP8_E4M3FN;
            Fp8E5M2Unet = value == UNetPrecisionMode.FP8_E5M2;
            Fp8E8M0FnuUnet = value == UNetPrecisionMode.FP8_E8M0FNU;

            // 触发命令更新
            UpdateStartCommand();
        }
    }

    partial void OnVaePrecisionModeChanged(VAEPrecisionMode value)
    {
        if (!_isUpdatingCommand)
        {
            // 同步更新对应的布尔属性（保持向后兼容）
            Fp16Vae = value == VAEPrecisionMode.FP16;
            Fp32Vae = value == VAEPrecisionMode.FP32;
            Bf16Vae = value == VAEPrecisionMode.BF16;
            CpuVae = value == VAEPrecisionMode.CPU;

            // 触发命令更新
            UpdateStartCommand();
        }
    }

    partial void OnAttentionAlgorithmModeChanged(AttentionAlgorithmMode value)
    {
        if (!_isUpdatingCommand)
        {
            // 同步更新对应的布尔属性（保持向后兼容）
            UseSplitCrossAttention = value == AttentionAlgorithmMode.SplitCrossAttention;
            UseQuadCrossAttention = value == AttentionAlgorithmMode.QuadCrossAttention;
            UsePytorchCrossAttention = value == AttentionAlgorithmMode.PytorchCrossAttention;
            UseSageAttention = value == AttentionAlgorithmMode.SageAttention;
            UseFlashAttention = value == AttentionAlgorithmMode.FlashAttention;

            // 触发命令更新
            UpdateStartCommand();
        }
    }

    partial void OnCacheModeChanged(CacheMode value)
    {
        if (!_isUpdatingCommand)
        {
            // 同步更新对应的布尔属性（保持向后兼容）
            CacheClassic = value == CacheMode.Classic;
            CacheNone = value == CacheMode.None;

            // 如果选择LRU模式且CacheLru为0，设置默认值
            if (value == CacheMode.LRU && CacheLru == 0)
            {
                CacheLru = 1; // 设置一个默认的LRU缓存大小
            }

            // 触发命令更新
            UpdateStartCommand();
        }
    }

    partial void OnTextEncoderPrecisionModeChanged(TextEncoderPrecisionMode value)
    {
        if (!_isUpdatingCommand)
        {
            // 同步更新对应的布尔属性（保持向后兼容）
            Fp8E4M3FnTextEnc = value == TextEncoderPrecisionMode.FP8_E4M3FN;
            Fp8E5M2TextEnc = value == TextEncoderPrecisionMode.FP8_E5M2;
            Fp16TextEnc = value == TextEncoderPrecisionMode.FP16;
            Fp32TextEnc = value == TextEncoderPrecisionMode.FP32;
            Bf16TextEnc = value == TextEncoderPrecisionMode.BF16;

            // 触发命令更新
            UpdateStartCommand();
        }
    }

    partial void OnGlobalPrecisionForceModeChanged(GlobalPrecisionForceMode value)
    {
        if (!_isUpdatingCommand)
        {
            // 同步更新对应的布尔属性（保持向后兼容）
            ForceFp32 = value == GlobalPrecisionForceMode.ForceFP32;
            ForceFp16 = value == GlobalPrecisionForceMode.ForceFP16;

            // 触发命令更新
            UpdateStartCommand();
        }
    }

    partial void OnCudaMemoryAllocatorModeChanged(CudaMemoryAllocatorMode value)
    {
        if (!_isUpdatingCommand)
        {
            // 同步更新对应的布尔属性（保持向后兼容）
            CudaMalloc = value == CudaMemoryAllocatorMode.CudaMalloc;
            DisableCudaMalloc = value == CudaMemoryAllocatorMode.DisableCudaMalloc;

            // 触发命令更新
            UpdateStartCommand();
        }
    }

    partial void OnAttentionUpcastModeChanged(AttentionUpcastMode value)
    {
        if (!_isUpdatingCommand)
        {
            // 同步更新对应的布尔属性（保持向后兼容）
            ForceUpcastAttention = value == AttentionUpcastMode.ForceUpcast;
            DontUpcastAttention = value == AttentionUpcastMode.DontUpcast;

            // 触发命令更新
            UpdateStartCommand();
        }
    }

    partial void OnBrowserAutoLaunchModeChanged(BrowserAutoLaunchMode value)
    {
        if (!_isUpdatingCommand)
        {
            // 同步更新对应的布尔属性（保持向后兼容）
            AutoLaunch = value == BrowserAutoLaunchMode.AutoLaunch;
            DisableAutoLaunch = value == BrowserAutoLaunchMode.DisableAutoLaunch;

            // 触发命令更新
            UpdateStartCommand();
        }
    }

    /// <summary>
    /// 根据布尔属性的当前值同步设置枚举值（用于加载项目后保证一致性）
    /// </summary>
    private void SyncEnumValuesFromBooleans()
    {
        // 同步内存管理模式
        if (GpuOnly) MemoryManagementMode = MemoryManagementMode.GpuOnly;
        else if (HighVram) MemoryManagementMode = MemoryManagementMode.HighVram;
        else if (NormalVram) MemoryManagementMode = MemoryManagementMode.NormalVram;
        else if (LowVramMode) MemoryManagementMode = MemoryManagementMode.LowVram;
        else if (NoVram) MemoryManagementMode = MemoryManagementMode.NoVram;
        else if (CpuMode) MemoryManagementMode = MemoryManagementMode.CpuMode;
        else MemoryManagementMode = MemoryManagementMode.None;

        // 同步UNet精度模式
        if (Fp32Unet) UnetPrecisionMode = UNetPrecisionMode.FP32;
        else if (Fp64Unet) UnetPrecisionMode = UNetPrecisionMode.FP64;
        else if (Bf16Unet) UnetPrecisionMode = UNetPrecisionMode.BF16;
        else if (Fp16Unet) UnetPrecisionMode = UNetPrecisionMode.FP16;
        else if (Fp8E4M3FnUnet) UnetPrecisionMode = UNetPrecisionMode.FP8_E4M3FN;
        else if (Fp8E5M2Unet) UnetPrecisionMode = UNetPrecisionMode.FP8_E5M2;
        else if (Fp8E8M0FnuUnet) UnetPrecisionMode = UNetPrecisionMode.FP8_E8M0FNU;
        else UnetPrecisionMode = UNetPrecisionMode.None;

        // 同步VAE精度模式
        if (Fp16Vae) VaePrecisionMode = VAEPrecisionMode.FP16;
        else if (Fp32Vae) VaePrecisionMode = VAEPrecisionMode.FP32;
        else if (Bf16Vae) VaePrecisionMode = VAEPrecisionMode.BF16;
        else if (CpuVae) VaePrecisionMode = VAEPrecisionMode.CPU;
        else VaePrecisionMode = VAEPrecisionMode.None;

        // 同步注意力算法模式
        if (UseSplitCrossAttention) AttentionAlgorithmMode = AttentionAlgorithmMode.SplitCrossAttention;
        else if (UseQuadCrossAttention) AttentionAlgorithmMode = AttentionAlgorithmMode.QuadCrossAttention;
        else if (UsePytorchCrossAttention) AttentionAlgorithmMode = AttentionAlgorithmMode.PytorchCrossAttention;
        else if (UseSageAttention) AttentionAlgorithmMode = AttentionAlgorithmMode.SageAttention;
        else if (UseFlashAttention) AttentionAlgorithmMode = AttentionAlgorithmMode.FlashAttention;
        else AttentionAlgorithmMode = AttentionAlgorithmMode.None;

        // 同步缓存模式
        if (CacheClassic) CacheMode = CacheMode.Classic;
        else if (CacheNone) CacheMode = CacheMode.None;
        else if (CacheLru > 0) CacheMode = CacheMode.LRU;
        else CacheMode = CacheMode.Default;

        // 同步文本编码器精度模式
        if (Fp8E4M3FnTextEnc) TextEncoderPrecisionMode = TextEncoderPrecisionMode.FP8_E4M3FN;
        else if (Fp8E5M2TextEnc) TextEncoderPrecisionMode = TextEncoderPrecisionMode.FP8_E5M2;
        else if (Fp16TextEnc) TextEncoderPrecisionMode = TextEncoderPrecisionMode.FP16;
        else if (Fp32TextEnc) TextEncoderPrecisionMode = TextEncoderPrecisionMode.FP32;
        else if (Bf16TextEnc) TextEncoderPrecisionMode = TextEncoderPrecisionMode.BF16;
        else TextEncoderPrecisionMode = TextEncoderPrecisionMode.None;

        // 同步全局精度强制模式
        if (ForceFp32) GlobalPrecisionForceMode = GlobalPrecisionForceMode.ForceFP32;
        else if (ForceFp16) GlobalPrecisionForceMode = GlobalPrecisionForceMode.ForceFP16;
        else GlobalPrecisionForceMode = GlobalPrecisionForceMode.None;

        // 同步CUDA内存分配器模式
        if (CudaMalloc) CudaMemoryAllocatorMode = CudaMemoryAllocatorMode.CudaMalloc;
        else if (DisableCudaMalloc) CudaMemoryAllocatorMode = CudaMemoryAllocatorMode.DisableCudaMalloc;
        else CudaMemoryAllocatorMode = CudaMemoryAllocatorMode.Default;

        // 同步注意力上投模式
        if (ForceUpcastAttention) AttentionUpcastMode = AttentionUpcastMode.ForceUpcast;
        else if (DontUpcastAttention) AttentionUpcastMode = AttentionUpcastMode.DontUpcast;
        else AttentionUpcastMode = AttentionUpcastMode.Default;

        // 同步浏览器自动启动模式
        if (AutoLaunch) BrowserAutoLaunchMode = BrowserAutoLaunchMode.AutoLaunch;
        else if (DisableAutoLaunch) BrowserAutoLaunchMode = BrowserAutoLaunchMode.DisableAutoLaunch;
        else BrowserAutoLaunchMode = BrowserAutoLaunchMode.Default;
    }

    [ObservableProperty]
    private bool _lowVramMode = false;

    [ObservableProperty]
    private bool _cpuMode = false;

    partial void OnCpuModeChanged(bool value)
    {
        // CPU模式变化时立即更新启动命令
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                // CPU模式互斥其他模式
                GpuOnly = false;
                HighVram = false;
                NormalVram = false;
                LowVramMode = false;
                NoVram = false;
            }
            UpdateStartCommand();
        }
    }

    // === 内存管理相关变化处理 ===
    partial void OnGpuOnlyChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                // GPU-only模式互斥其他模式
                HighVram = false;
                NormalVram = false;
                LowVramMode = false;
                NoVram = false;
                CpuMode = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnHighVramChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                // High VRAM模式互斥其他模式
                GpuOnly = false;
                NormalVram = false;
                LowVramMode = false;
                NoVram = false;
                CpuMode = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnNormalVramChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                // Normal VRAM模式互斥其他模式
                GpuOnly = false;
                HighVram = false;
                LowVramMode = false;
                NoVram = false;
                CpuMode = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnLowVramModeChanged(bool value)
    {
        // 低显存模式变化时立即更新启动命令
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                // Low VRAM模式互斥其他模式
                GpuOnly = false;
                HighVram = false;
                NormalVram = false;
                NoVram = false;
                CpuMode = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnNoVramChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                // No VRAM模式互斥其他模式
                GpuOnly = false;
                HighVram = false;
                NormalVram = false;
                LowVramMode = false;
                CpuMode = false;
            }
            UpdateStartCommand();
        }
    }

    // === 精度设置变化处理 ===
    partial void OnForceFp32Changed(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                ForceFp16 = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnForceFp16Changed(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                ForceFp32 = false;
                Fp16Unet = true; // 根据cli_args.py逻辑
            }
            UpdateStartCommand();
        }
    }

    // UNet精度互斥组
    partial void OnFp32UnetChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                Fp64Unet = false;
                Bf16Unet = false;
                Fp16Unet = false;
                Fp8E4M3FnUnet = false;
                Fp8E5M2Unet = false;
                Fp8E8M0FnuUnet = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnFp64UnetChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                Fp32Unet = false;
                Bf16Unet = false;
                Fp16Unet = false;
                Fp8E4M3FnUnet = false;
                Fp8E5M2Unet = false;
                Fp8E8M0FnuUnet = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnBf16UnetChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                Fp32Unet = false;
                Fp64Unet = false;
                Fp16Unet = false;
                Fp8E4M3FnUnet = false;
                Fp8E5M2Unet = false;
                Fp8E8M0FnuUnet = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnFp16UnetChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                Fp32Unet = false;
                Fp64Unet = false;
                Bf16Unet = false;
                Fp8E4M3FnUnet = false;
                Fp8E5M2Unet = false;
                Fp8E8M0FnuUnet = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnFp8E4M3FnUnetChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                Fp32Unet = false;
                Fp64Unet = false;
                Bf16Unet = false;
                Fp16Unet = false;
                Fp8E5M2Unet = false;
                Fp8E8M0FnuUnet = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnFp8E5M2UnetChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                Fp32Unet = false;
                Fp64Unet = false;
                Bf16Unet = false;
                Fp16Unet = false;
                Fp8E4M3FnUnet = false;
                Fp8E8M0FnuUnet = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnFp8E8M0FnuUnetChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                Fp32Unet = false;
                Fp64Unet = false;
                Bf16Unet = false;
                Fp16Unet = false;
                Fp8E4M3FnUnet = false;
                Fp8E5M2Unet = false;
            }
            UpdateStartCommand();
        }
    }

    // VAE精度互斥组
    partial void OnFp16VaeChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                Fp32Vae = false;
                Bf16Vae = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnFp32VaeChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                Fp16Vae = false;
                Bf16Vae = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnBf16VaeChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                Fp16Vae = false;
                Fp32Vae = false;
            }
            UpdateStartCommand();
        }
    }

    // 文本编码器精度变化处理
    partial void OnFp8E4M3FnTextEncChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnFp8E5M2TextEncChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnFp16TextEncChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnFp32TextEncChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnBf16TextEncChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    // 缓存设置互斥组
    partial void OnCacheClassicChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                CacheNone = false;
                CacheLru = 0;
            }
            UpdateStartCommand();
        }
    }

    partial void OnCacheNoneChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                CacheClassic = false;
                CacheLru = 0;
            }
            UpdateStartCommand();
        }
    }

    partial void OnCacheLruChanged(int value)
    {
        if (!_isUpdatingCommand)
        {
            // 验证缓存LRU大小：必须大于等于0，建议不超过1000
            if (value < 0)
            {
                CacheLru = 0; // 恢复默认值
                return;
            }
            if (value > 1000)
            {
                CacheLru = 1000; // 限制最大值
            }

            if (value > 0)
            {
                CacheClassic = false;
                CacheNone = false;
            }
            UpdateStartCommand();
        }
    }

    // 注意力机制互斥组
    partial void OnUseSplitCrossAttentionChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                UseQuadCrossAttention = false;
                UsePytorchCrossAttention = false;
                UseSageAttention = false;
                UseFlashAttention = false;
                DisableXformers = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnUseQuadCrossAttentionChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                UseSplitCrossAttention = false;
                UsePytorchCrossAttention = false;
                UseSageAttention = false;
                UseFlashAttention = false;
                DisableXformers = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnUsePytorchCrossAttentionChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                UseSplitCrossAttention = false;
                UseQuadCrossAttention = false;
                UseSageAttention = false;
                UseFlashAttention = false;
                DisableXformers = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnUseSageAttentionChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                UseSplitCrossAttention = false;
                UseQuadCrossAttention = false;
                UsePytorchCrossAttention = false;
                UseFlashAttention = false;
                DisableXformers = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnUseFlashAttentionChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                UseSplitCrossAttention = false;
                UseQuadCrossAttention = false;
                UsePytorchCrossAttention = false;
                UseSageAttention = false;
                DisableXformers = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnDisableXformersChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    // Upcast注意力互斥组
    partial void OnForceUpcastAttentionChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                DontUpcastAttention = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnDontUpcastAttentionChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                ForceUpcastAttention = false;
            }
            UpdateStartCommand();
        }
    }

    // CUDA malloc互斥组
    partial void OnCudaMallocChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                DisableCudaMalloc = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnDisableCudaMallocChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                CudaMalloc = false;
            }
            UpdateStartCommand();
        }
    }

    // 其他重要设置变化处理
    partial void OnListenAddressChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnAutoLaunchChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                DisableAutoLaunch = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnDisableAutoLaunchChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            if (value)
            {
                AutoLaunch = false;
            }
            UpdateStartCommand();
        }
    }

    partial void OnBaseDirectoryChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnOutputDirectoryChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnTempDirectoryChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnInputDirectoryChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnUserDirectoryChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnExtraModelPathsConfigChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnTlsKeyFileChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnTlsCertFileChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnEnableCorsHeaderChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnCorsOriginChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnMaxUploadSizeChanged(float value)
    {
        if (!_isUpdatingCommand)
        {
            // 验证最大上传大小：必须大于0，建议不超过10GB (10240MB)
            if (value < 0)
            {
                MaxUploadSize = 100; // 恢复默认值
                return;
            }
            if (value > 10240)
            {
                MaxUploadSize = 10240; // 限制最大值为10GB
            }
            UpdateStartCommand();
        }
    }

    partial void OnCudaDeviceChanged(int? value)
    {
        if (!_isUpdatingCommand)
        {
            // 验证CUDA设备ID：如果有值，必须大于等于0，建议不超过15
            if (value.HasValue)
            {
                if (value.Value < 0)
                {
                    CudaDevice = null; // 清除无效值
                    return;
                }
                if (value.Value > 15)
                {
                    CudaDevice = 15; // 限制最大设备ID为15
                }
            }
            UpdateStartCommand();
        }
    }

    partial void OnDefaultDeviceChanged(int? value)
    {
        if (!_isUpdatingCommand)
        {
            // 验证默认设备ID：如果有值，必须大于等于0，建议不超过15
            if (value.HasValue)
            {
                if (value.Value < 0)
                {
                    DefaultDevice = null; // 清除无效值
                    return;
                }
                if (value.Value > 15)
                {
                    DefaultDevice = 15; // 限制最大设备ID为15
                }
            }
            UpdateStartCommand();
        }
    }

    partial void OnDirectmlDeviceChanged(int? value)
    {
        if (!_isUpdatingCommand)
        {
            // 验证directML设备ID：如果有值，可以是-1(自动检测)或0-15的设备ID
            if (value.HasValue)
            {
                if (value.Value < -1)
                {
                    DirectmlDevice = null; // 清除无效值
                    return;
                }
                if (value.Value > 15)
                {
                    DirectmlDevice = 15; // 限制最大设备ID为15
                }
            }
            UpdateStartCommand();
        }
    }

    partial void OnOneApiDeviceSelectorChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnDisableIpexOptimizeChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnSupportsFp8ComputeChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnPreviewMethodChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            if (!string.IsNullOrEmpty(value) && value.Contains("ComboBoxItem:"))
            {
                var idx = value.LastIndexOf(':');
                if (idx >= 0 && idx < value.Length - 1)
                {
                    var cleaned = value[(idx + 1)..].Trim();
                    if (!string.Equals(cleaned, value, StringComparison.Ordinal))
                    {
                        _isUpdatingCommand = true;
                        try { PreviewMethod = cleaned; }
                        finally { _isUpdatingCommand = false; }
                    }
                }
            }
            UpdateStartCommand();
        }
    }

    partial void OnPreviewSizeChanged(int value)
    {
        if (!_isUpdatingCommand)
        {
            // 验证预览尺寸：必须大于0，建议在64-2048之间
            if (value <= 0)
            {
                PreviewSize = 512; // 恢复默认值
                return;
            }
            if (value < 64)
            {
                PreviewSize = 64; // 最小值64
            }
            else if (value > 2048)
            {
                PreviewSize = 2048; // 最大值2048
            }
            UpdateStartCommand();
        }
    }

    partial void OnReserveVramChanged(float? value)
    {
        if (!_isUpdatingCommand)
        {
            // 验证显存预留：如果有值，必须大于等于0，建议不超过32GB (32768MB)
            if (value.HasValue)
            {
                if (value.Value < 0)
                {
                    ReserveVram = null; // 清除无效值
                    return;
                }
                if (value.Value > 32768)
                {
                    ReserveVram = 32768; // 限制最大值为32GB
                }
            }
            UpdateStartCommand();
        }
    }

    partial void OnAsyncOffloadChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnForceNonBlockingChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnDisableSmartMemoryChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnCpuVaeChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnForceChannelsLastChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnDeterministicChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnFastFp16AccumulationChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnFastFp8MatrixMultChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnFastCublasOpsChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnMmapTorchFilesChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnDisableMmapChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnDefaultHashingFunctionChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnVerboseChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            // 清洗可能的 "System.Windows.Controls.ComboBoxItem: LEVEL" 字符串
            if (!string.IsNullOrEmpty(value) && value.Contains("ComboBoxItem:"))
            {
                var idx = value.LastIndexOf(':');
                if (idx >= 0 && idx < value.Length - 1)
                {
                    var cleaned = value[(idx + 1)..].Trim();
                    if (!string.Equals(cleaned, value, StringComparison.Ordinal))
                    {
                        _isUpdatingCommand = true;
                        try { Verbose = cleaned; }
                        finally { _isUpdatingCommand = false; }
                    }
                }
            }
            UpdateStartCommand();
        }
    }

    partial void OnLogStdoutChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnDontPrintServerChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnQuickTestForCiChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnWindowsStandaloneBuildChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnMultiUserChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnDisableMetadataChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnDisableAllCustomNodesChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnWhitelistCustomNodesChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnDisableApiNodesChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnFrontEndVersionChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnFrontEndRootChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnEnableCompressResponseBodyChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnComfyApiBaseChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnDatabaseUrlChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    partial void OnExtraArgsChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    [ObservableProperty]
    private string _modelsPath = "./models";

    partial void OnModelsPathChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    [ObservableProperty]
    private string _outputPath = "./output";

    partial void OnOutputPathChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    [ObservableProperty]
    private string _extraArgs = string.Empty;

    [ObservableProperty]
    private string _customNodesPath = "./custom_nodes";

    partial void OnCustomNodesPathChanged(string value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    [ObservableProperty]
    private bool _autoLoadWorkflow = true;

    partial void OnAutoLoadWorkflowChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    [ObservableProperty]
    private bool _enableWorkflowSnapshots = false;

    partial void OnEnableWorkflowSnapshotsChanged(bool value)
    {
        if (!_isUpdatingCommand)
        {
            UpdateStartCommand();
        }
    }

    // === 基础设置 ===
    [ObservableProperty]
    private string _listenAddress = "127.0.0.1";

    [ObservableProperty]
    private string _tlsKeyFile = string.Empty;

    [ObservableProperty]
    private string _tlsCertFile = string.Empty;

    [ObservableProperty]
    private bool _enableCorsHeader = false;

    [ObservableProperty]
    private string _corsOrigin = "*";

    [ObservableProperty]
    private float _maxUploadSize = 100;

    // === 目录设置 ===
    [ObservableProperty]
    private string _baseDirectory = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _tempDirectory = string.Empty;

    [ObservableProperty]
    private string _inputDirectory = string.Empty;

    [ObservableProperty]
    private string _userDirectory = string.Empty;

    [ObservableProperty]
    private string _extraModelPathsConfig = string.Empty;

    // === 启动设置 ===
    [ObservableProperty]
    private bool _autoLaunch = false;

    [ObservableProperty]
    private bool _disableAutoLaunch = false;

    // === GPU/CUDA设置 ===
    [ObservableProperty]
    private int? _cudaDevice = null;

    [ObservableProperty]
    private int? _defaultDevice = null;

    [ObservableProperty]
    private bool _cudaMalloc = false;

    [ObservableProperty]
    private bool _disableCudaMalloc = false;

    [ObservableProperty]
    private int? _directmlDevice = null;

    [ObservableProperty]
    private string _oneApiDeviceSelector = string.Empty;

    [ObservableProperty]
    private bool _disableIpexOptimize = false;

    [ObservableProperty]
    private bool _supportsFp8Compute = false;

    // === 精度设置 ===
    [ObservableProperty]
    private bool _forceFp32 = false;

    [ObservableProperty]
    private bool _forceFp16 = false;

    [ObservableProperty]
    private UNetPrecisionMode _unetPrecisionMode = UNetPrecisionMode.None;

    [ObservableProperty]
    private bool _fp32Unet = false;

    [ObservableProperty]
    private bool _fp64Unet = false;

    [ObservableProperty]
    private bool _bf16Unet = false;

    [ObservableProperty]
    private bool _fp16Unet = false;

    [ObservableProperty]
    private bool _fp8E4M3FnUnet = false;

    [ObservableProperty]
    private bool _fp8E5M2Unet = false;

    [ObservableProperty]
    private bool _fp8E8M0FnuUnet = false;

    [ObservableProperty]
    private VAEPrecisionMode _vaePrecisionMode = VAEPrecisionMode.None;

    [ObservableProperty]
    private bool _fp16Vae = false;

    [ObservableProperty]
    private bool _fp32Vae = false;

    [ObservableProperty]
    private bool _bf16Vae = false;

    [ObservableProperty]
    private bool _cpuVae = false;

    [ObservableProperty]
    private bool _fp8E4M3FnTextEnc = false;

    [ObservableProperty]
    private bool _fp8E5M2TextEnc = false;

    [ObservableProperty]
    private bool _fp16TextEnc = false;

    [ObservableProperty]
    private bool _fp32TextEnc = false;

    [ObservableProperty]
    private bool _bf16TextEnc = false;

    [ObservableProperty]
    private bool _forceChannelsLast = false;

    // === 内存管理 ===
    [ObservableProperty]
    private MemoryManagementMode _memoryManagementMode = MemoryManagementMode.None;

    [ObservableProperty]
    private bool _gpuOnly = false;

    [ObservableProperty]
    private bool _highVram = false;

    [ObservableProperty]
    private bool _normalVram = false;

    [ObservableProperty]
    private bool _noVram = false;

    [ObservableProperty]
    private float? _reserveVram = null;

    [ObservableProperty]
    private bool _asyncOffload = false;

    [ObservableProperty]
    private bool _forceNonBlocking = false;

    [ObservableProperty]
    private bool _disableSmartMemory = false;

    // === 预览设置 ===
    [ObservableProperty]
    private string _previewMethod = "none";

    [ObservableProperty]
    private int _previewSize = 512;

    // === 缓存设置 ===
    [ObservableProperty]
    private CacheMode _cacheMode = CacheMode.Default;

    [ObservableProperty]
    private bool _cacheClassic = false;

    [ObservableProperty]
    private int _cacheLru = 0;

    [ObservableProperty]
    private bool _cacheNone = false;

    // === 注意力机制设置 ===
    [ObservableProperty]
    private AttentionAlgorithmMode _attentionAlgorithmMode = AttentionAlgorithmMode.None;

    [ObservableProperty]
    private bool _useSplitCrossAttention = false;

    [ObservableProperty]
    private bool _useQuadCrossAttention = false;

    [ObservableProperty]
    private bool _usePytorchCrossAttention = false;

    [ObservableProperty]
    private bool _useSageAttention = false;

    [ObservableProperty]
    private bool _useFlashAttention = false;

    [ObservableProperty]
    private bool _disableXformers = false;

    [ObservableProperty]
    private bool _forceUpcastAttention = false;

    // === 新增的枚举属性（互斥选项组） ===
    [ObservableProperty]
    private TextEncoderPrecisionMode _textEncoderPrecisionMode = TextEncoderPrecisionMode.None;

    [ObservableProperty]
    private GlobalPrecisionForceMode _globalPrecisionForceMode = GlobalPrecisionForceMode.None;

    [ObservableProperty]
    private CudaMemoryAllocatorMode _cudaMemoryAllocatorMode = CudaMemoryAllocatorMode.Default;

    [ObservableProperty]
    private AttentionUpcastMode _attentionUpcastMode = AttentionUpcastMode.Default;

    [ObservableProperty]
    private BrowserAutoLaunchMode _browserAutoLaunchMode = BrowserAutoLaunchMode.Default;

    [ObservableProperty]
    private bool _dontUpcastAttention = false;

    // === 性能设置 ===
    [ObservableProperty]
    private bool _deterministic = false;

    [ObservableProperty]
    private bool _fastFp16Accumulation = false;

    [ObservableProperty]
    private bool _fastFp8MatrixMult = false;

    [ObservableProperty]
    private bool _fastCublasOps = false;

    [ObservableProperty]
    private bool _mmapTorchFiles = false;

    [ObservableProperty]
    private bool _disableMmap = false;

    // === 哈希设置 ===
    [ObservableProperty]
    private string _defaultHashingFunction = "sha256";

    // === 调试和日志设置 ===
    [ObservableProperty]
    private bool _dontPrintServer = false;

    [ObservableProperty]
    private bool _quickTestForCi = false;

    [ObservableProperty]
    private bool _windowsStandaloneBuild = false;

    [ObservableProperty]
    private string _verbose = "INFO";

    [ObservableProperty]
    private bool _logStdout = false;

    // === 元数据和自定义节点 ===
    [ObservableProperty]
    private bool _disableMetadata = false;

    [ObservableProperty]
    private bool _disableAllCustomNodes = false;

    [ObservableProperty]
    private string _whitelistCustomNodes = string.Empty;

    [ObservableProperty]
    private bool _disableApiNodes = false;

    // === 多用户设置 ===
    [ObservableProperty]
    private bool _multiUser = false;

    // === 前端设置 ===
    [ObservableProperty]
    private string _frontEndVersion = "comfyanonymous/ComfyUI@latest";

    [ObservableProperty]
    private string _frontEndRoot = string.Empty;

    [ObservableProperty]
    private bool _enableCompressResponseBody = false;

    [ObservableProperty]
    private string _comfyApiBase = "https://api.comfy.org";

    [ObservableProperty]
    private string _databaseUrl = string.Empty;

    [ObservableProperty]
    private string _tagsString = "AI绘画,图像生成,工作流,节点编辑";

    public event EventHandler<Project>? ProjectSaved;
    public event EventHandler<string>? ProjectDeleted;
    public event EventHandler? DialogCancelled;

    public ComfyUIProjectSettingsViewModel(IProjectService projectService, ILanguageService languageService)
    {
        _projectService = projectService;
        _languageService = languageService;

        // 确保有默认的启动命令（始终使用 ComfyUI 根目录下的 main.py）
        if (string.IsNullOrEmpty(StartCommand))
        {
            StartCommand = "python main.py";
        }

        // 初始化启动命令建议（固定为 main.py，不再支持自定义脚本文件名）
        UpdateStartCommandSuggestions();
    }

    private void UpdateStartCommandSuggestions()
    {
        var pythonCommand = string.IsNullOrEmpty(PythonPath) || !IsValidPythonPath(PythonPath) ? "python" : $"\"{PythonPath}\"";
        var currentCommand = StartCommand;

        StartCommandSuggestions.Clear();
        StartCommandSuggestions.Add($"{pythonCommand} main.py");
        StartCommandSuggestions.Add($"{pythonCommand} main.py --listen");
        StartCommandSuggestions.Add($"{pythonCommand} main.py --port 8188");
        StartCommandSuggestions.Add($"{pythonCommand} main.py --listen --port 8188");
        StartCommandSuggestions.Add($"{pythonCommand} main.py --cpu");
        StartCommandSuggestions.Add($"{pythonCommand} main.py --directml");
        StartCommandSuggestions.Add($"{pythonCommand} main.py --lowvram");
        StartCommandSuggestions.Add($"{pythonCommand} main.py --listen --lowvram");

        // 如果当前命令不在建议列表中，将其添加到列表首位
        if (!string.IsNullOrEmpty(currentCommand) && !StartCommandSuggestions.Contains(currentCommand))
        {
            StartCommandSuggestions.Insert(0, currentCommand);
        }
    }

    private void UpdateStartCommand()
    {
        UpdateRunCommand();
        UpdateCommandLineArguments();
        UpdateCompleteStartCommand();
    }

    /// <summary>
    /// 更新运行命令部分
    /// </summary>
    private void UpdateRunCommand()
    {
        _isUpdatingCommand = true;
        try
        {
            // 使用正则表达式更新Python路径，只有有效路径才使用
            var pythonCommand = string.IsNullOrEmpty(PythonPath) || !IsValidPythonPath(PythonPath) ? "python" : $"\"{PythonPath}\"";

            // 启动脚本固定为 ComfyUI 根目录下的 main.py
            // 若指定了有效的 ComfyUIRootPath，则使用其下的 main.py 作为绝对路径；否则使用相对路径 main.py
            string scriptToUse;
            if (!string.IsNullOrWhiteSpace(ComfyUIRootPath) && Directory.Exists(ComfyUIRootPath))
            {
                try
                {
                    var combined = Path.Combine(ComfyUIRootPath, "main.py");
                    scriptToUse = File.Exists(combined) ? $"\"{combined}\"" : "main.py";
                }
                catch
                {
                    scriptToUse = "main.py";
                }
            }
            else
            {
                scriptToUse = "main.py";
            }

            RunCommand = $"{pythonCommand} {scriptToUse}";
            OnPropertyChanged(nameof(RunCommand));
        }
        finally
        {
            _isUpdatingCommand = false;
        }
    }

    /// <summary>
    /// 更新命令行参数部分
    /// </summary>
    private void UpdateCommandLineArguments()
    {
        _isUpdatingCommand = true;
        try
        {
            var arguments = new List<string>();

            // === 基础网络设置 ===
            // --listen参数
            if (ListenAllInterfaces)
            {
                if (!string.IsNullOrEmpty(ListenAddress) && ListenAddress != "127.0.0.1")
                {
                    arguments.Add($"--listen {ListenAddress}");
                }
                else
                {
                    arguments.Add("--listen");
                }
            }

            // 端口设置
            var portValue = Port ?? 8188;
            if (portValue != 8188)
            {
                arguments.Add($"--port {portValue}");
            }

            // TLS设置
            if (!string.IsNullOrEmpty(TlsKeyFile))
                arguments.Add($"--tls-keyfile \"{TlsKeyFile}\"");
            if (!string.IsNullOrEmpty(TlsCertFile))
                arguments.Add($"--tls-certfile \"{TlsCertFile}\"");

            // CORS设置
            if (EnableCorsHeader)
            {
                if (!string.IsNullOrEmpty(CorsOrigin) && CorsOrigin != "*")
                    arguments.Add($"--enable-cors-header {CorsOrigin}");
                else
                    arguments.Add("--enable-cors-header");
            }

            // 上传大小设置
            if (MaxUploadSize != 100)
                arguments.Add($"--max-upload-size {MaxUploadSize}");

            // === 目录设置 ===
            if (!string.IsNullOrEmpty(BaseDirectory))
                arguments.Add($"--base-directory \"{BaseDirectory}\"");
            if (!string.IsNullOrEmpty(OutputDirectory))
                arguments.Add($"--output-directory \"{OutputDirectory}\"");
            if (!string.IsNullOrEmpty(TempDirectory))
                arguments.Add($"--temp-directory \"{TempDirectory}\"");
            if (!string.IsNullOrEmpty(InputDirectory))
                arguments.Add($"--input-directory \"{InputDirectory}\"");
            if (!string.IsNullOrEmpty(UserDirectory))
                arguments.Add($"--user-directory \"{UserDirectory}\"");
            if (!string.IsNullOrEmpty(ExtraModelPathsConfig))
                arguments.Add($"--extra-model-paths-config \"{ExtraModelPathsConfig}\"");

            // === 启动设置 ===
            if (AutoLaunch)
                arguments.Add("--auto-launch");
            if (DisableAutoLaunch)
                arguments.Add("--disable-auto-launch");

            // === GPU/CUDA设置 ===
            if (CudaDevice.HasValue)
                arguments.Add($"--cuda-device {CudaDevice.Value}");
            if (DefaultDevice.HasValue)
                arguments.Add($"--default-device {DefaultDevice.Value}");
            if (CudaMalloc)
                arguments.Add("--cuda-malloc");
            if (DisableCudaMalloc)
                arguments.Add("--disable-cuda-malloc");
            if (DirectmlDevice.HasValue)
            {
                if (DirectmlDevice.Value == -1)
                    arguments.Add("--directml");
                else
                    arguments.Add($"--directml {DirectmlDevice.Value}");
            }
            if (!string.IsNullOrEmpty(OneApiDeviceSelector))
                arguments.Add($"--oneapi-device-selector {OneApiDeviceSelector}");
            if (DisableIpexOptimize)
                arguments.Add("--disable-ipex-optimize");
            if (SupportsFp8Compute)
                arguments.Add("--supports-fp8-compute");

            // === 精度设置 ===
            if (ForceFp32)
                arguments.Add("--force-fp32");
            if (ForceFp16)
                arguments.Add("--force-fp16");

            // UNet精度
            if (Fp32Unet)
                arguments.Add("--fp32-unet");
            if (Fp64Unet)
                arguments.Add("--fp64-unet");
            if (Bf16Unet)
                arguments.Add("--bf16-unet");
            if (Fp16Unet)
                arguments.Add("--fp16-unet");
            if (Fp8E4M3FnUnet)
                arguments.Add("--fp8_e4m3fn-unet");
            if (Fp8E5M2Unet)
                arguments.Add("--fp8_e5m2-unet");
            if (Fp8E8M0FnuUnet)
                arguments.Add("--fp8_e8m0fnu-unet");

            // VAE精度
            if (Fp16Vae)
                arguments.Add("--fp16-vae");
            if (Fp32Vae)
                arguments.Add("--fp32-vae");
            if (Bf16Vae)
                arguments.Add("--bf16-vae");
            if (CpuVae)
                arguments.Add("--cpu-vae");

            // 文本编码器精度
            if (Fp8E4M3FnTextEnc)
                arguments.Add("--fp8_e4m3fn-text-enc");
            if (Fp8E5M2TextEnc)
                arguments.Add("--fp8_e5m2-text-enc");
            if (Fp16TextEnc)
                arguments.Add("--fp16-text-enc");
            if (Fp32TextEnc)
                arguments.Add("--fp32-text-enc");
            if (Bf16TextEnc)
                arguments.Add("--bf16-text-enc");

            if (ForceChannelsLast)
                arguments.Add("--force-channels-last");

            // === 内存管理 ===
            if (GpuOnly)
                arguments.Add("--gpu-only");
            if (HighVram)
                arguments.Add("--highvram");
            if (NormalVram)
                arguments.Add("--normalvram");
            if (LowVramMode)
                arguments.Add("--lowvram");
            if (NoVram)
                arguments.Add("--novram");
            if (CpuMode)
                arguments.Add("--cpu");
            if (ReserveVram.HasValue)
                arguments.Add($"--reserve-vram {ReserveVram.Value}");
            if (AsyncOffload)
                arguments.Add("--async-offload");
            if (ForceNonBlocking)
                arguments.Add("--force-non-blocking");
            if (DisableSmartMemory)
                arguments.Add("--disable-smart-memory");

            // === 预览设置 ===
            if (PreviewMethod != "none")
                arguments.Add($"--preview-method {PreviewMethod}");
            if (PreviewSize != 512)
                arguments.Add($"--preview-size {PreviewSize}");

            // === 缓存设置 ===
            if (CacheClassic)
                arguments.Add("--cache-classic");
            if (CacheLru > 0)
                arguments.Add($"--cache-lru {CacheLru}");
            if (CacheNone)
                arguments.Add("--cache-none");

            // === 注意力机制 ===
            if (UseSplitCrossAttention)
                arguments.Add("--use-split-cross-attention");
            if (UseQuadCrossAttention)
                arguments.Add("--use-quad-cross-attention");
            if (UsePytorchCrossAttention)
                arguments.Add("--use-pytorch-cross-attention");
            if (UseSageAttention)
                arguments.Add("--use-sage-attention");
            if (UseFlashAttention)
                arguments.Add("--use-flash-attention");
            if (DisableXformers)
                arguments.Add("--disable-xformers");
            if (ForceUpcastAttention)
                arguments.Add("--force-upcast-attention");
            if (DontUpcastAttention)
                arguments.Add("--dont-upcast-attention");

            // === 性能设置 ===
            if (Deterministic)
                arguments.Add("--deterministic");

            // Fast模式设置
            var fastArgs = new List<string>();
            if (FastFp16Accumulation) fastArgs.Add("fp16_accumulation");
            if (FastFp8MatrixMult) fastArgs.Add("fp8_matrix_mult");
            if (FastCublasOps) fastArgs.Add("cublas_ops");

            if (fastArgs.Any())
                arguments.Add($"--fast {string.Join(" ", fastArgs)}");

            if (MmapTorchFiles)
                arguments.Add("--mmap-torch-files");
            if (DisableMmap)
                arguments.Add("--disable-mmap");

            // === 哈希设置 ===
            if (DefaultHashingFunction != "sha256")
                arguments.Add($"--default-hashing-function {DefaultHashingFunction}");

            // === 调试和日志设置 ===
            if (DontPrintServer)
                arguments.Add("--dont-print-server");
            if (QuickTestForCi)
                arguments.Add("--quick-test-for-ci");
            if (WindowsStandaloneBuild)
                arguments.Add("--windows-standalone-build");
            if (Verbose != "INFO")
                arguments.Add($"--verbose {Verbose}");
            if (LogStdout)
                arguments.Add("--log-stdout");

            // === 元数据和自定义节点 ===
            if (DisableMetadata)
                arguments.Add("--disable-metadata");
            if (DisableAllCustomNodes)
                arguments.Add("--disable-all-custom-nodes");
            if (!string.IsNullOrEmpty(WhitelistCustomNodes))
                arguments.Add($"--whitelist-custom-nodes {WhitelistCustomNodes}");
            if (DisableApiNodes)
                arguments.Add("--disable-api-nodes");

            // === 多用户设置 ===
            if (MultiUser)
                arguments.Add("--multi-user");

            // === 前端设置 ===
            if (FrontEndVersion != "comfyanonymous/ComfyUI@latest")
                arguments.Add($"--front-end-version {FrontEndVersion}");
            if (!string.IsNullOrEmpty(FrontEndRoot))
                arguments.Add($"--front-end-root \"{FrontEndRoot}\"");
            if (EnableCompressResponseBody)
                arguments.Add("--enable-compress-response-body");
            if (ComfyApiBase != "https://api.comfy.org")
                arguments.Add($"--comfy-api-base {ComfyApiBase}");
            if (!string.IsNullOrEmpty(DatabaseUrl))
                arguments.Add($"--database-url \"{DatabaseUrl}\"");

            // 添加额外参数（如果有）
            if (!string.IsNullOrEmpty(ExtraArgs))
                arguments.Add(ExtraArgs.Trim());

            // 生成最终的命令行参数字符串
            CommandLineArguments = string.Join(" ", arguments);
            OnPropertyChanged(nameof(CommandLineArguments));
        }
        finally
        {
            _isUpdatingCommand = false;
        }
    }

    private void UpdateArgument(ref string command, string pattern, bool condition, string argument)
    {
        if (condition)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(command, pattern))
            {
                command += $" {argument}";
            }
            else
            {
                command = System.Text.RegularExpressions.Regex.Replace(command, pattern, $" {argument}");
            }
        }
        else
        {
            command = System.Text.RegularExpressions.Regex.Replace(command, pattern, "");
        }
    }

    public void LoadProject(Project project)
    {
        _isUpdatingCommand = true;
        try
        {
            _project = project;

            ProjectName = project.Name ?? string.Empty;
            ProjectPath = project.LocalPath ?? string.Empty;
            StartCommand = project.StartCommand ?? string.Empty; // 保留空
            TagsString = project.Tags != null ? string.Join(", ", project.Tags) : "AI绘画,图像生成,工作流,节点编辑";

            // 解析ComfyUI特定的启动参数（用于兼容老数据），空则跳过
            if (!string.IsNullOrWhiteSpace(project.StartCommand))
            {
                ParseStartCommand(project.StartCommand);
            }

            // 如果存在持久化的 ComfyUISettings，用其覆盖解析结果
            if (project.ComfyUISettings != null)
            {
                var settings = project.ComfyUISettings;

                // 基础设置
                ListenAddress = settings.ListenAddress;
                ListenAllInterfaces = settings.ListenAllInterfaces;
                Port = settings.Port;
                TlsKeyFile = settings.TlsKeyFile;
                TlsCertFile = settings.TlsCertFile;
                EnableCorsHeader = settings.EnableCorsHeader;
                CorsOrigin = settings.CorsOrigin;
                MaxUploadSize = settings.MaxUploadSize;

                // Python 设置（启动脚本固定为 main.py，不再从设置中恢复脚本名）

                // 互斥选项组（下拉菜单）
                MemoryManagementMode = settings.MemoryManagementMode;
                UnetPrecisionMode = settings.UNetPrecisionMode;
                VaePrecisionMode = settings.VAEPrecisionMode;
                AttentionAlgorithmMode = settings.AttentionAlgorithmMode;
                CacheMode = settings.CacheMode;

                // 目录设置
                BaseDirectory = settings.BaseDirectory;
                OutputDirectory = settings.OutputDirectory;
                TempDirectory = settings.TempDirectory;
                InputDirectory = settings.InputDirectory;
                UserDirectory = settings.UserDirectory;
                ExtraModelPathsConfig = settings.ExtraModelPathsConfig;

                // 启动设置
                AutoLaunch = settings.AutoLaunch;
                DisableAutoLaunch = settings.DisableAutoLaunch;

                // GPU/CUDA设置
                CudaDevice = settings.CudaDevice;
                DefaultDevice = settings.DefaultDevice;
                CudaMalloc = settings.CudaMalloc;
                DisableCudaMalloc = settings.DisableCudaMalloc;
                DirectmlDevice = settings.DirectmlDevice;
                OneApiDeviceSelector = settings.OneApiDeviceSelector;
                DisableIpexOptimize = settings.DisableIpexOptimize;
                SupportsFp8Compute = settings.SupportsFp8Compute;

                // 精度设置
                ForceFp32 = settings.ForceFp32;
                ForceFp16 = settings.ForceFp16;
                Fp32Unet = settings.Fp32Unet;
                Fp64Unet = settings.Fp64Unet;
                Bf16Unet = settings.Bf16Unet;
                Fp16Unet = settings.Fp16Unet;
                Fp8E4M3FnUnet = settings.Fp8E4M3FnUnet;
                Fp8E5M2Unet = settings.Fp8E5M2Unet;
                Fp8E8M0FnuUnet = settings.Fp8E8M0FnuUnet;
                Fp16Vae = settings.Fp16Vae;
                Fp32Vae = settings.Fp32Vae;
                Bf16Vae = settings.Bf16Vae;
                CpuVae = settings.CpuVae;
                Fp8E4M3FnTextEnc = settings.Fp8E4M3FnTextEnc;
                Fp8E5M2TextEnc = settings.Fp8E5M2TextEnc;
                Fp16TextEnc = settings.Fp16TextEnc;
                Fp32TextEnc = settings.Fp32TextEnc;
                Bf16TextEnc = settings.Bf16TextEnc;
                ForceChannelsLast = settings.ForceChannelsLast;

                // 内存管理
                GpuOnly = settings.GpuOnly;
                HighVram = settings.HighVram;
                NormalVram = settings.NormalVram;
                LowVramMode = settings.LowVramMode;
                NoVram = settings.NoVram;
                CpuMode = settings.CpuMode;
                ReserveVram = settings.ReserveVram;
                AsyncOffload = settings.AsyncOffload;
                ForceNonBlocking = settings.ForceNonBlocking;
                DisableSmartMemory = settings.DisableSmartMemory;

                // 预览设置
                PreviewMethod = settings.PreviewMethod;
                PreviewSize = settings.PreviewSize;

                // 缓存设置
                CacheClassic = settings.CacheClassic;
                CacheLru = settings.CacheLru;
                CacheNone = settings.CacheNone;

                // 注意力机制设置
                UseSplitCrossAttention = settings.UseSplitCrossAttention;
                UseQuadCrossAttention = settings.UseQuadCrossAttention;
                UsePytorchCrossAttention = settings.UsePytorchCrossAttention;
                UseSageAttention = settings.UseSageAttention;
                UseFlashAttention = settings.UseFlashAttention;
                DisableXformers = settings.DisableXformers;
                ForceUpcastAttention = settings.ForceUpcastAttention;
                DontUpcastAttention = settings.DontUpcastAttention;

                // 性能设置
                Deterministic = settings.Deterministic;
                FastFp16Accumulation = settings.FastFp16Accumulation;
                FastFp8MatrixMult = settings.FastFp8MatrixMult;
                FastCublasOps = settings.FastCublasOps;
                MmapTorchFiles = settings.MmapTorchFiles;
                DisableMmap = settings.DisableMmap;

                // 哈希设置
                DefaultHashingFunction = settings.DefaultHashingFunction;

                // 调试和日志设置
                DontPrintServer = settings.DontPrintServer;
                QuickTestForCi = settings.QuickTestForCi;
                WindowsStandaloneBuild = settings.WindowsStandaloneBuild;
                Verbose = settings.Verbose;
                LogStdout = settings.LogStdout;

                // 元数据和自定义节点
                DisableMetadata = settings.DisableMetadata;
                DisableAllCustomNodes = settings.DisableAllCustomNodes;
                WhitelistCustomNodes = settings.WhitelistCustomNodes;
                DisableApiNodes = settings.DisableApiNodes;

                // 多用户设置
                MultiUser = settings.MultiUser;

                // 前端设置
                FrontEndVersion = settings.FrontEndVersion;
                FrontEndRoot = settings.FrontEndRoot;
                EnableCompressResponseBody = settings.EnableCompressResponseBody;
                ComfyApiBase = settings.ComfyApiBase;
                DatabaseUrl = settings.DatabaseUrl;

                // 路径设置（保留原有设置）
                ComfyUIRootPath = settings.ComfyUIRootPath;
                PythonPath = settings.PythonPath;
                ModelsPath = settings.ModelsPath;
                OutputPath = settings.OutputPath;
                ExtraArgs = settings.ExtraArgs;
                CustomNodesPath = settings.CustomNodesPath;
                AutoLoadWorkflow = settings.AutoLoadWorkflow;
                EnableWorkflowSnapshots = settings.EnableWorkflowSnapshots;
            }

            // 同步枚举值和布尔值（确保一致性）
            SyncEnumValuesFromBooleans();

            // 更新启动命令建议
            UpdateStartCommandSuggestions();

            // 根据加载的设置重新生成启动命令（确保StartCommand包含所有配置的参数）
            UpdateStartCommand();
        }
        finally
        {
            _isUpdatingCommand = false;
        }
    }

    private void ParseStartCommand(string command)
    {
        // 设置解析标志，防止递归更新
        _isUpdatingCommand = true;
        try
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                ResetToDefaults();
                return;
            }

            // 解析Python路径
            var pythonMatch = System.Text.RegularExpressions.Regex.Match(command, @"^""?([^""]+?)""?\s+main\.py", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (pythonMatch.Success)
            {
                var extractedPath = pythonMatch.Groups[1].Value.Trim('"');
                if (!extractedPath.Equals("python", StringComparison.OrdinalIgnoreCase))
                {
                    PythonPath = extractedPath;
                }
                else
                {
                    PythonPath = string.Empty;
                }
            }

            // === 基础网络设置 ===
            ListenAllInterfaces = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--listen\b");

            var listenMatch = System.Text.RegularExpressions.Regex.Match(command, @"--listen\s+([^\s]+)");
            if (listenMatch.Success)
            {
                ListenAddress = listenMatch.Groups[1].Value;
            }
            else if (ListenAllInterfaces)
            {
                ListenAddress = "127.0.0.1"; // 默认值
            }

            var portMatch = System.Text.RegularExpressions.Regex.Match(command, @"--port\s+(\d+)");
            Port = portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out int port) ? port : 8188;

            // TLS设置
            var tlsKeyMatch = System.Text.RegularExpressions.Regex.Match(command, @"--tls-keyfile\s+""?([^""\s]+)""?");
            TlsKeyFile = tlsKeyMatch.Success ? tlsKeyMatch.Groups[1].Value.Trim('"') : string.Empty;

            var tlsCertMatch = System.Text.RegularExpressions.Regex.Match(command, @"--tls-certfile\s+""?([^""\s]+)""?");
            TlsCertFile = tlsCertMatch.Success ? tlsCertMatch.Groups[1].Value.Trim('"') : string.Empty;

            EnableCorsHeader = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--enable-cors-header\b");
            var corsMatch = System.Text.RegularExpressions.Regex.Match(command, @"--enable-cors-header\s+([^\s]+)");
            CorsOrigin = corsMatch.Success ? corsMatch.Groups[1].Value : "*";

            var maxUploadMatch = System.Text.RegularExpressions.Regex.Match(command, @"--max-upload-size\s+([\d.]+)");
            MaxUploadSize = maxUploadMatch.Success && float.TryParse(maxUploadMatch.Groups[1].Value, out float maxUpload) ? maxUpload : 100;

            // === 目录设置 ===
            var baseDirMatch = System.Text.RegularExpressions.Regex.Match(command, @"--base-directory\s+""?([^""\s]+)""?");
            BaseDirectory = baseDirMatch.Success ? baseDirMatch.Groups[1].Value.Trim('"') : string.Empty;

            var outputDirMatch = System.Text.RegularExpressions.Regex.Match(command, @"--output-directory\s+""?([^""\s]+)""?");
            OutputDirectory = outputDirMatch.Success ? outputDirMatch.Groups[1].Value.Trim('"') : string.Empty;

            var tempDirMatch = System.Text.RegularExpressions.Regex.Match(command, @"--temp-directory\s+""?([^""\s]+)""?");
            TempDirectory = tempDirMatch.Success ? tempDirMatch.Groups[1].Value.Trim('"') : string.Empty;

            var inputDirMatch = System.Text.RegularExpressions.Regex.Match(command, @"--input-directory\s+""?([^""\s]+)""?");
            InputDirectory = inputDirMatch.Success ? inputDirMatch.Groups[1].Value.Trim('"') : string.Empty;

            var userDirMatch = System.Text.RegularExpressions.Regex.Match(command, @"--user-directory\s+""?([^""\s]+)""?");
            UserDirectory = userDirMatch.Success ? userDirMatch.Groups[1].Value.Trim('"') : string.Empty;

            var extraModelMatch = System.Text.RegularExpressions.Regex.Match(command, @"--extra-model-paths-config\s+""?([^""\s]+)""?");
            ExtraModelPathsConfig = extraModelMatch.Success ? extraModelMatch.Groups[1].Value.Trim('"') : string.Empty;

            // === 启动设置 ===
            AutoLaunch = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--auto-launch\b");
            DisableAutoLaunch = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--disable-auto-launch\b");

            // === GPU/CUDA设置 ===
            var cudaDeviceMatch = System.Text.RegularExpressions.Regex.Match(command, @"--cuda-device\s+(\d+)");
            CudaDevice = cudaDeviceMatch.Success && int.TryParse(cudaDeviceMatch.Groups[1].Value, out int cudaDev) ? cudaDev : null;

            var defaultDeviceMatch = System.Text.RegularExpressions.Regex.Match(command, @"--default-device\s+(\d+)");
            DefaultDevice = defaultDeviceMatch.Success && int.TryParse(defaultDeviceMatch.Groups[1].Value, out int defDev) ? defDev : null;

            CudaMalloc = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--cuda-malloc\b");
            DisableCudaMalloc = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--disable-cuda-malloc\b");

            var directmlMatch = System.Text.RegularExpressions.Regex.Match(command, @"--directml(?:\s+(\d+))?\b");
            if (directmlMatch.Success)
            {
                DirectmlDevice = directmlMatch.Groups[1].Success && int.TryParse(directmlMatch.Groups[1].Value, out int dmlDev) ? dmlDev : -1;
            }
            else
            {
                DirectmlDevice = null;
            }

            var oneApiMatch = System.Text.RegularExpressions.Regex.Match(command, @"--oneapi-device-selector\s+([^\s]+)");
            OneApiDeviceSelector = oneApiMatch.Success ? oneApiMatch.Groups[1].Value : string.Empty;

            DisableIpexOptimize = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--disable-ipex-optimize\b");
            SupportsFp8Compute = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--supports-fp8-compute\b");

            // === 精度设置 ===
            ForceFp32 = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--force-fp32\b");
            ForceFp16 = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--force-fp16\b");

            // UNet精度
            Fp32Unet = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp32-unet\b");
            Fp64Unet = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp64-unet\b");
            Bf16Unet = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--bf16-unet\b");
            Fp16Unet = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp16-unet\b");
            Fp8E4M3FnUnet = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp8_e4m3fn-unet\b");
            Fp8E5M2Unet = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp8_e5m2-unet\b");
            Fp8E8M0FnuUnet = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp8_e8m0fnu-unet\b");

            // VAE精度
            Fp16Vae = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp16-vae\b");
            Fp32Vae = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp32-vae\b");
            Bf16Vae = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--bf16-vae\b");
            CpuVae = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--cpu-vae\b");

            // 文本编码器精度
            Fp8E4M3FnTextEnc = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp8_e4m3fn-text-enc\b");
            Fp8E5M2TextEnc = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp8_e5m2-text-enc\b");
            Fp16TextEnc = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp16-text-enc\b");
            Fp32TextEnc = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--fp32-text-enc\b");
            Bf16TextEnc = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--bf16-text-enc\b");

            ForceChannelsLast = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--force-channels-last\b");

            // === 内存管理 ===
            GpuOnly = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--gpu-only\b");
            HighVram = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--highvram\b");
            NormalVram = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--normalvram\b");
            LowVramMode = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--lowvram\b");
            NoVram = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--novram\b");
            CpuMode = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--cpu\b");

            var reserveVramMatch = System.Text.RegularExpressions.Regex.Match(command, @"--reserve-vram\s+([\d.]+)");
            ReserveVram = reserveVramMatch.Success && float.TryParse(reserveVramMatch.Groups[1].Value, out float reserveVram) ? reserveVram : null;

            AsyncOffload = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--async-offload\b");
            ForceNonBlocking = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--force-non-blocking\b");
            DisableSmartMemory = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--disable-smart-memory\b");

            // === 预览设置 ===
            var previewMethodMatch = System.Text.RegularExpressions.Regex.Match(command, @"--preview-method\s+(\w+)");
            PreviewMethod = previewMethodMatch.Success ? previewMethodMatch.Groups[1].Value : "none";

            var previewSizeMatch = System.Text.RegularExpressions.Regex.Match(command, @"--preview-size\s+(\d+)");
            PreviewSize = previewSizeMatch.Success && int.TryParse(previewSizeMatch.Groups[1].Value, out int previewSize) ? previewSize : 512;

            // === 缓存设置 ===
            CacheClassic = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--cache-classic\b");
            CacheNone = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--cache-none\b");

            var cacheLruMatch = System.Text.RegularExpressions.Regex.Match(command, @"--cache-lru\s+(\d+)");
            CacheLru = cacheLruMatch.Success && int.TryParse(cacheLruMatch.Groups[1].Value, out int cacheLru) ? cacheLru : 0;

            // === 注意力机制 ===
            UseSplitCrossAttention = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--use-split-cross-attention\b");
            UseQuadCrossAttention = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--use-quad-cross-attention\b");
            UsePytorchCrossAttention = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--use-pytorch-cross-attention\b");
            UseSageAttention = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--use-sage-attention\b");
            UseFlashAttention = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--use-flash-attention\b");
            DisableXformers = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--disable-xformers\b");
            ForceUpcastAttention = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--force-upcast-attention\b");
            DontUpcastAttention = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--dont-upcast-attention\b");

            // === 性能设置 ===
            Deterministic = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--deterministic\b");

            // Fast模式解析
            var fastMatch = System.Text.RegularExpressions.Regex.Match(command, @"--fast\s+(.+?)(?:\s+--|\s*$)");
            if (fastMatch.Success)
            {
                var fastArgs = fastMatch.Groups[1].Value;
                FastFp16Accumulation = fastArgs.Contains("fp16_accumulation");
                FastFp8MatrixMult = fastArgs.Contains("fp8_matrix_mult");
                FastCublasOps = fastArgs.Contains("cublas_ops");
            }
            else
            {
                FastFp16Accumulation = false;
                FastFp8MatrixMult = false;
                FastCublasOps = false;
            }

            MmapTorchFiles = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--mmap-torch-files\b");
            DisableMmap = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--disable-mmap\b");

            // === 哈希设置 ===
            var hashFunctionMatch = System.Text.RegularExpressions.Regex.Match(command, @"--default-hashing-function\s+(\w+)");
            DefaultHashingFunction = hashFunctionMatch.Success ? hashFunctionMatch.Groups[1].Value : "sha256";

            // === 调试和日志设置 ===
            DontPrintServer = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--dont-print-server\b");
            QuickTestForCi = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--quick-test-for-ci\b");
            WindowsStandaloneBuild = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--windows-standalone-build\b");

            var verboseMatch = System.Text.RegularExpressions.Regex.Match(command, @"--verbose\s+(\w+)");
            Verbose = verboseMatch.Success ? verboseMatch.Groups[1].Value : "INFO";

            LogStdout = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--log-stdout\b");

            // === 元数据和自定义节点 ===
            DisableMetadata = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--disable-metadata\b");
            DisableAllCustomNodes = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--disable-all-custom-nodes\b");

            var whitelistMatch = System.Text.RegularExpressions.Regex.Match(command, @"--whitelist-custom-nodes\s+([^\s]+)");
            WhitelistCustomNodes = whitelistMatch.Success ? whitelistMatch.Groups[1].Value : string.Empty;

            DisableApiNodes = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--disable-api-nodes\b");

            // === 多用户设置 ===
            MultiUser = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--multi-user\b");

            // === 前端设置 ===
            var frontEndVersionMatch = System.Text.RegularExpressions.Regex.Match(command, @"--front-end-version\s+([^\s]+)");
            FrontEndVersion = frontEndVersionMatch.Success ? frontEndVersionMatch.Groups[1].Value : "comfyanonymous/ComfyUI@latest";

            var frontEndRootMatch = System.Text.RegularExpressions.Regex.Match(command, @"--front-end-root\s+""?([^""\s]+)""?");
            FrontEndRoot = frontEndRootMatch.Success ? frontEndRootMatch.Groups[1].Value.Trim('"') : string.Empty;

            EnableCompressResponseBody = System.Text.RegularExpressions.Regex.IsMatch(command, @"\b--enable-compress-response-body\b");

            var comfyApiMatch = System.Text.RegularExpressions.Regex.Match(command, @"--comfy-api-base\s+([^\s]+)");
            ComfyApiBase = comfyApiMatch.Success ? comfyApiMatch.Groups[1].Value : "https://api.comfy.org";

            var databaseUrlMatch = System.Text.RegularExpressions.Regex.Match(command, @"--database-url\s+""?([^""\s]+)""?");
            DatabaseUrl = databaseUrlMatch.Success ? databaseUrlMatch.Groups[1].Value.Trim('"') : string.Empty;

            // 解析剩余的参数作为ExtraArgs
            ParseExtraArgs(command);

            // 更新Python路径有效性状态
            IsPythonPathValid = IsValidPythonPath(PythonPath);
        }
        finally
        {
            _isUpdatingCommand = false;
        }
    }

    private void ResetToDefaults()
    {
        // 重置所有设置到默认值
        ListenAllInterfaces = false;
        ListenAddress = "127.0.0.1";
        Port = 8188;
        TlsKeyFile = string.Empty;
        TlsCertFile = string.Empty;
        EnableCorsHeader = false;
        CorsOrigin = "*";
        MaxUploadSize = 100;
        BaseDirectory = string.Empty;
        OutputDirectory = string.Empty;
        TempDirectory = string.Empty;
        InputDirectory = string.Empty;
        UserDirectory = string.Empty;
        ExtraModelPathsConfig = string.Empty;
        AutoLaunch = false;
        DisableAutoLaunch = false;
        CudaDevice = null;
        DefaultDevice = null;
        CudaMalloc = false;
        DisableCudaMalloc = false;
        DirectmlDevice = null;
        OneApiDeviceSelector = string.Empty;
        DisableIpexOptimize = false;
        SupportsFp8Compute = false;
        ForceFp32 = false;
        ForceFp16 = false;
        Fp32Unet = false;
        Fp64Unet = false;
        Bf16Unet = false;
        Fp16Unet = false;
        Fp8E4M3FnUnet = false;
        Fp8E5M2Unet = false;
        Fp8E8M0FnuUnet = false;
        Fp16Vae = false;
        Fp32Vae = false;
        Bf16Vae = false;
        CpuVae = false;
        Fp8E4M3FnTextEnc = false;
        Fp8E5M2TextEnc = false;
        Fp16TextEnc = false;
        Fp32TextEnc = false;
        Bf16TextEnc = false;
        ForceChannelsLast = false;
        GpuOnly = false;
        HighVram = false;
        NormalVram = false;
        LowVramMode = false;
        NoVram = false;
        CpuMode = false;
        ReserveVram = null;
        AsyncOffload = false;
        ForceNonBlocking = false;
        DisableSmartMemory = false;
        PreviewMethod = "none";
        PreviewSize = 512;
        CacheClassic = false;
        CacheLru = 0;
        CacheNone = false;
        UseSplitCrossAttention = false;
        UseQuadCrossAttention = false;
        UsePytorchCrossAttention = false;
        UseSageAttention = false;
        UseFlashAttention = false;
        DisableXformers = false;
        ForceUpcastAttention = false;
        DontUpcastAttention = false;
        Deterministic = false;
        FastFp16Accumulation = false;
        FastFp8MatrixMult = false;
        FastCublasOps = false;
        MmapTorchFiles = false;
        DisableMmap = false;
        DefaultHashingFunction = "sha256";
        DontPrintServer = false;
        QuickTestForCi = false;
        WindowsStandaloneBuild = false;
        Verbose = "INFO";
        LogStdout = false;
        DisableMetadata = false;
        DisableAllCustomNodes = false;
        WhitelistCustomNodes = string.Empty;
        DisableApiNodes = false;
        MultiUser = false;
        FrontEndVersion = "comfyanonymous/ComfyUI@latest";
        FrontEndRoot = string.Empty;
        EnableCompressResponseBody = false;
        ComfyApiBase = "https://api.comfy.org";
        DatabaseUrl = string.Empty;
        ExtraArgs = string.Empty;
    }

    private void ParseExtraArgs(string command)
    {
        // 提取所有已知的CLI参数，剩余的作为ExtraArgs
        var knownArgs = new[]
        {
            @"--listen(?:\s+[^\s]+)?", @"--port\s+\d+", @"--tls-keyfile\s+\S+", @"--tls-certfile\s+\S+",
            @"--enable-cors-header(?:\s+\S+)?", @"--max-upload-size\s+[\d.]+",
            @"--base-directory\s+\S+", @"--output-directory\s+\S+", @"--temp-directory\s+\S+",
            @"--input-directory\s+\S+", @"--user-directory\s+\S+", @"--extra-model-paths-config\s+\S+",
            @"--auto-launch", @"--disable-auto-launch",
            @"--cuda-device\s+\d+", @"--default-device\s+\d+", @"--cuda-malloc", @"--disable-cuda-malloc",
            @"--directml(?:\s+\d+)?", @"--oneapi-device-selector\s+\S+", @"--disable-ipex-optimize", @"--supports-fp8-compute",
            @"--force-fp32", @"--force-fp16",
            @"--fp32-unet", @"--fp64-unet", @"--bf16-unet", @"--fp16-unet",
            @"--fp8_e4m3fn-unet", @"--fp8_e5m2-unet", @"--fp8_e8m0fnu-unet",
            @"--fp16-vae", @"--fp32-vae", @"--bf16-vae", @"--cpu-vae",
            @"--fp8_e4m3fn-text-enc", @"--fp8_e5m2-text-enc", @"--fp16-text-enc", @"--fp32-text-enc", @"--bf16-text-enc",
            @"--force-channels-last",
            @"--gpu-only", @"--highvram", @"--normalvram", @"--lowvram", @"--novram", @"--cpu",
            @"--reserve-vram\s+[\d.]+", @"--async-offload", @"--force-non-blocking", @"--disable-smart-memory",
            @"--preview-method\s+\w+", @"--preview-size\s+\d+",
            @"--cache-classic", @"--cache-lru\s+\d+", @"--cache-none",
            @"--use-split-cross-attention", @"--use-quad-cross-attention", @"--use-pytorch-cross-attention",
            @"--use-sage-attention", @"--use-flash-attention", @"--disable-xformers",
            @"--force-upcast-attention", @"--dont-upcast-attention",
            @"--deterministic", @"--fast(?:\s+\S+)*", @"--mmap-torch-files", @"--disable-mmap",
            @"--default-hashing-function\s+\w+",
            @"--dont-print-server", @"--quick-test-for-ci", @"--windows-standalone-build",
            @"--verbose\s+\w+", @"--log-stdout",
            @"--disable-metadata", @"--disable-all-custom-nodes", @"--whitelist-custom-nodes\s+\S+", @"--disable-api-nodes",
            @"--multi-user",
            @"--front-end-version\s+\S+", @"--front-end-root\s+\S+", @"--enable-compress-response-body",
            @"--comfy-api-base\s+\S+", @"--database-url\s+\S+"
        };

        var remainingCommand = command;

        // 移除Python路径和main.py部分
        remainingCommand = System.Text.RegularExpressions.Regex.Replace(remainingCommand, @"^""?[^""]+?""?\s+main\.py\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // 移除所有已知参数
        foreach (var knownArg in knownArgs)
        {
            remainingCommand = System.Text.RegularExpressions.Regex.Replace(remainingCommand, knownArg, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // 清理多余空格并设置ExtraArgs
        ExtraArgs = System.Text.RegularExpressions.Regex.Replace(remainingCommand, @"\s+", " ").Trim();
    }

    [RelayCommand]
    private async Task BrowsePythonPath()
    {
        // 优先使用当前项目目录作为初始目录；若不可用，使用 PythonPath 的目录；最后回退到 ProgramFiles
        string? initialDir = null;

        if (!string.IsNullOrEmpty(ProjectPath))
        {
            try { initialDir = ProjectPath; }
            catch { initialDir = null; }
        }

        if (string.IsNullOrEmpty(initialDir) && !string.IsNullOrEmpty(PythonPath))
        {
            try { initialDir = Path.GetDirectoryName(PythonPath); }
            catch { initialDir = null; }
        }

        if (string.IsNullOrEmpty(initialDir))
        {
            initialDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        }

        var fileTypes = new List<FilePickerFileType>
        {
            new FilePickerFileType("Python executable (python.exe)") { Patterns = new[] { "python.exe" } }
        };

        var result = await BrowseFileAsync(
            _languageService.GetString("ComfyUI_SelectPythonExecutable"),
            initialDir,
            fileTypes);

        if (result != null)
        {
            // 验证选择的文件是否为python.exe
            if (Path.GetFileName(result).Equals("python.exe", StringComparison.OrdinalIgnoreCase))
            {
                PythonPath = result;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(_languageService.GetString("ComfyUI_OnlyPythonExeAllowed"));
            }
        }
    }

    /// <summary>
    /// 自动根据项目路径和常见目录结构检测 ComfyUI 根目录（包含 main.py 的目录）。
    /// </summary>
    [RelayCommand]
    private void DetectComfyUIRoot()
    {
        if (_project == null)
        {
            return;
        }

        try
        {
            string? detected = null;
            var baseDir = _project.LocalPath;

            if (!string.IsNullOrWhiteSpace(baseDir) && Directory.Exists(baseDir))
            {
                // 情况 1：项目根目录本身就是 ComfyUI 根目录（包含 main.py）
                var mainInRoot = Path.Combine(baseDir, "main.py");
                if (File.Exists(mainInRoot))
                {
                    detected = baseDir;
                }
                else
                {
                    // 情况 2：常见子目录名下存在 main.py
                    var candidates = new[] { "ComfyUI", "comfyui" };
                    foreach (var sub in candidates)
                    {
                        var subRoot = Path.Combine(baseDir, sub);
                        var mainInSub = Path.Combine(subRoot, "main.py");
                        if (Directory.Exists(subRoot) && File.Exists(mainInSub))
                        {
                            detected = subRoot;
                            break;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(detected))
            {
                ComfyUIRootPath = detected;
                // 根目录变化会影响脚本完整路径，更新运行命令
                UpdateRunCommand();
            }
            else
            {
                // 未检测到则保持为空，让用户手动选择
            }
        }
        catch
        {
            // 忽略异常，避免影响 UI
        }
    }

    /// <summary>
    /// 手动浏览选择 ComfyUI 根目录。
    /// </summary>
    [RelayCommand]
    private async Task BrowseComfyUIRoot()
    {
        var initialDir = !string.IsNullOrWhiteSpace(ComfyUIRootPath) && Directory.Exists(ComfyUIRootPath)
            ? ComfyUIRootPath
            : (!string.IsNullOrWhiteSpace(ProjectPath) ? ProjectPath : null);

        var result = await BrowseFolderAsync(
            _languageService.GetString("ComfyUI_SelectComfyUIDirectory"),
            initialDir);

        if (!string.IsNullOrWhiteSpace(result))
        {
            ComfyUIRootPath = result;
            UpdateRunCommand();
        }
    }

    [RelayCommand]
    private async Task BrowseModelsPath()
    {
        var initialDir = !string.IsNullOrWhiteSpace(ModelsPath) && Directory.Exists(ModelsPath) ? ModelsPath : null;
        var result = await BrowseFolderAsync(_languageService.GetString("ComfyUI_SelectModelsFolder"), initialDir);
        if (result != null)
        {
            ModelsPath = result;
        }
    }

    [RelayCommand]
    private async Task BrowseOutputPath()
    {
        var initialDir = !string.IsNullOrWhiteSpace(OutputPath) && Directory.Exists(OutputPath) ? OutputPath : null;
        var result = await BrowseFolderAsync(_languageService.GetString("ComfyUI_SelectOutputFolder"), initialDir);
        if (result != null)
        {
            OutputPath = result;
        }
    }

    [RelayCommand]
    private async Task BrowseCustomNodesPath()
    {
        var initialDir = !string.IsNullOrWhiteSpace(CustomNodesPath) && Directory.Exists(CustomNodesPath) ? CustomNodesPath : null;
        var result = await BrowseFolderAsync(_languageService.GetString("ComfyUI_SelectCustomNodesFolder"), initialDir);
        if (result != null)
        {
            CustomNodesPath = result;
        }
    }

    [RelayCommand]
    private async Task BrowseBaseDirectory()
    {
        var initialDir = !string.IsNullOrWhiteSpace(BaseDirectory) && Directory.Exists(BaseDirectory) ? BaseDirectory : null;
        var result = await BrowseFolderAsync(_languageService.GetString("ComfyUI_SelectBaseDirectory"), initialDir);
        if (result != null)
        {
            BaseDirectory = result;
        }
    }

    [RelayCommand]
    private async Task BrowseInputDirectory()
    {
        var initialDir = !string.IsNullOrWhiteSpace(InputDirectory) && Directory.Exists(InputDirectory) ? InputDirectory : null;
        var result = await BrowseFolderAsync(_languageService.GetString("ComfyUI_SelectInputFolder"), initialDir);
        if (result != null)
        {
            InputDirectory = result;
        }
    }

    [RelayCommand]
    private async Task BrowseTempDirectory()
    {
        var initialDir = !string.IsNullOrWhiteSpace(TempDirectory) && Directory.Exists(TempDirectory) ? TempDirectory : null;
        var result = await BrowseFolderAsync(_languageService.GetString("ComfyUI_SelectTempFolder"), initialDir);
        if (result != null)
        {
            TempDirectory = result;
        }
    }

    [RelayCommand]
    private async Task BrowseUserDirectory()
    {
        var initialDir = !string.IsNullOrWhiteSpace(UserDirectory) && Directory.Exists(UserDirectory) ? UserDirectory : null;
        var result = await BrowseFolderAsync(_languageService.GetString("ComfyUI_SelectUserDirectory"), initialDir);
        if (result != null)
        {
            UserDirectory = result;
        }
    }

    [RelayCommand]
    private async Task BrowseExtraModelPathsConfig()
    {
        var fileTypes = new List<FilePickerFileType>
        {
            new FilePickerFileType("YAML files (*.yaml)") { Patterns = new[] { "*.yaml" } },
            new FilePickerFileType("All files (*.*)") { Patterns = new[] { "*" } }
        };

        var result = await BrowseFileAsync(
            _languageService.GetString("ComfyUI_SelectExtraModelPathsConfig"),
            null,
            fileTypes);

        if (result != null)
        {
            ExtraModelPathsConfig = result;
        }
    }

    [RelayCommand]
    private async Task BrowseTlsKeyFile()
    {
        var fileTypes = new List<FilePickerFileType>
        {
            new FilePickerFileType("Key files (*.key)") { Patterns = new[] { "*.key" } },
            new FilePickerFileType("All files (*.*)") { Patterns = new[] { "*" } }
        };

        var result = await BrowseFileAsync(
            _languageService.GetString("ComfyUI_SelectTLSKeyFile"),
            null,
            fileTypes);

        if (result != null)
        {
            TlsKeyFile = result;
        }
    }

    [RelayCommand]
    private async Task BrowseTlsCertFile()
    {
        var fileTypes = new List<FilePickerFileType>
        {
            new FilePickerFileType("Certificate files (*.crt, *.pem)") { Patterns = new[] { "*.crt", "*.pem" } },
            new FilePickerFileType("All files (*.*)") { Patterns = new[] { "*" } }
        };

        var result = await BrowseFileAsync(
            _languageService.GetString("ComfyUI_SelectTLSCertFile"),
            null,
            fileTypes);

        if (result != null)
        {
            TlsCertFile = result;
        }
    }

    [RelayCommand]
    private async Task BrowseFrontEndRoot()
    {
        var initialDir = !string.IsNullOrWhiteSpace(FrontEndRoot) && Directory.Exists(FrontEndRoot) ? FrontEndRoot : null;
        var result = await BrowseFolderAsync(_languageService.GetString("ComfyUI_SelectFrontendRoot"), initialDir);
        if (result != null)
        {
            FrontEndRoot = result;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_project == null) return;

        try
        {
            // 更新项目信息
            _project.StartCommand = StartCommand;
            _project.LastModified = DateTime.Now;

            // 更新 ComfyUI 设置持久化（仅在框架为 ComfyUI 时）
            if (_project.Framework.Equals("ComfyUI", StringComparison.OrdinalIgnoreCase))
            {
                _project.ComfyUISettings = new ComfyUISettings
                {
                    // 基础设置
                    ListenAddress = ListenAddress,
                    ListenAllInterfaces = ListenAllInterfaces,
                    Port = Port ?? 8188, // 如果为空则使用默认值
                    TlsKeyFile = TlsKeyFile,
                    TlsCertFile = TlsCertFile,
                    EnableCorsHeader = EnableCorsHeader,
                    CorsOrigin = CorsOrigin,
                    MaxUploadSize = MaxUploadSize,

                    // Python 设置（启动脚本固定为 main.py，不再单独持久化脚本名）

                    // 互斥选项组（下拉菜单）
                    MemoryManagementMode = MemoryManagementMode,
                    UNetPrecisionMode = UnetPrecisionMode,
                    VAEPrecisionMode = VaePrecisionMode,
                    AttentionAlgorithmMode = AttentionAlgorithmMode,
                    CacheMode = CacheMode,

                    // 目录设置
                    BaseDirectory = BaseDirectory,
                    OutputDirectory = OutputDirectory,
                    TempDirectory = TempDirectory,
                    InputDirectory = InputDirectory,
                    UserDirectory = UserDirectory,
                    ExtraModelPathsConfig = ExtraModelPathsConfig,

                    // 启动设置
                    AutoLaunch = AutoLaunch,
                    DisableAutoLaunch = DisableAutoLaunch,

                    // GPU/CUDA设置
                    CudaDevice = CudaDevice,
                    DefaultDevice = DefaultDevice,
                    CudaMalloc = CudaMalloc,
                    DisableCudaMalloc = DisableCudaMalloc,
                    DirectmlDevice = DirectmlDevice,
                    OneApiDeviceSelector = OneApiDeviceSelector,
                    DisableIpexOptimize = DisableIpexOptimize,
                    SupportsFp8Compute = SupportsFp8Compute,

                    // 精度设置
                    ForceFp32 = ForceFp32,
                    ForceFp16 = ForceFp16,
                    Fp32Unet = Fp32Unet,
                    Fp64Unet = Fp64Unet,
                    Bf16Unet = Bf16Unet,
                    Fp16Unet = Fp16Unet,
                    Fp8E4M3FnUnet = Fp8E4M3FnUnet,
                    Fp8E5M2Unet = Fp8E5M2Unet,
                    Fp8E8M0FnuUnet = Fp8E8M0FnuUnet,
                    Fp16Vae = Fp16Vae,
                    Fp32Vae = Fp32Vae,
                    Bf16Vae = Bf16Vae,
                    CpuVae = CpuVae,
                    Fp8E4M3FnTextEnc = Fp8E4M3FnTextEnc,
                    Fp8E5M2TextEnc = Fp8E5M2TextEnc,
                    Fp16TextEnc = Fp16TextEnc,
                    Fp32TextEnc = Fp32TextEnc,
                    Bf16TextEnc = Bf16TextEnc,
                    ForceChannelsLast = ForceChannelsLast,

                    // 内存管理
                    GpuOnly = GpuOnly,
                    HighVram = HighVram,
                    NormalVram = NormalVram,
                    LowVramMode = LowVramMode,
                    NoVram = NoVram,
                    CpuMode = CpuMode,
                    ReserveVram = ReserveVram,
                    AsyncOffload = AsyncOffload,
                    ForceNonBlocking = ForceNonBlocking,
                    DisableSmartMemory = DisableSmartMemory,

                    // 预览设置
                    PreviewMethod = PreviewMethod,
                    PreviewSize = PreviewSize,

                    // 缓存设置
                    CacheClassic = CacheClassic,
                    CacheLru = CacheLru,
                    CacheNone = CacheNone,

                    // 注意力机制设置
                    UseSplitCrossAttention = UseSplitCrossAttention,
                    UseQuadCrossAttention = UseQuadCrossAttention,
                    UsePytorchCrossAttention = UsePytorchCrossAttention,
                    UseSageAttention = UseSageAttention,
                    UseFlashAttention = UseFlashAttention,
                    DisableXformers = DisableXformers,
                    ForceUpcastAttention = ForceUpcastAttention,
                    DontUpcastAttention = DontUpcastAttention,

                    // 性能设置
                    Deterministic = Deterministic,
                    FastFp16Accumulation = FastFp16Accumulation,
                    FastFp8MatrixMult = FastFp8MatrixMult,
                    FastCublasOps = FastCublasOps,
                    MmapTorchFiles = MmapTorchFiles,
                    DisableMmap = DisableMmap,

                    // 哈希设置
                    DefaultHashingFunction = DefaultHashingFunction,

                    // 调试和日志设置
                    DontPrintServer = DontPrintServer,
                    QuickTestForCi = QuickTestForCi,
                    WindowsStandaloneBuild = WindowsStandaloneBuild,
                    Verbose = Verbose,
                    LogStdout = LogStdout,

                    // 元数据和自定义节点
                    DisableMetadata = DisableMetadata,
                    DisableAllCustomNodes = DisableAllCustomNodes,
                    WhitelistCustomNodes = WhitelistCustomNodes,
                    DisableApiNodes = DisableApiNodes,

                    // 多用户设置
                    MultiUser = MultiUser,

                    // 前端设置
                    FrontEndVersion = FrontEndVersion,
                    FrontEndRoot = FrontEndRoot,
                    EnableCompressResponseBody = EnableCompressResponseBody,
                    ComfyApiBase = ComfyApiBase,
                    DatabaseUrl = DatabaseUrl,

                    // 路径设置（保留原有设置）
                    ComfyUIRootPath = ComfyUIRootPath,
                    PythonPath = PythonPath,
                    ModelsPath = ModelsPath,
                    OutputPath = OutputPath,
                    ExtraArgs = ExtraArgs,
                    // custom_nodes 目录完全由 ComfyUI 根目录决定：<ComfyUIRootPath>\custom_nodes
                    CustomNodesPath = (!string.IsNullOrWhiteSpace(ComfyUIRootPath) && Directory.Exists(ComfyUIRootPath))
                        ? Path.Combine(ComfyUIRootPath, "custom_nodes")
                        : string.Empty,
                    AutoLoadWorkflow = AutoLoadWorkflow,
                    EnableWorkflowSnapshots = EnableWorkflowSnapshots
                };
            }

            // 解析标签
            if (!string.IsNullOrWhiteSpace(TagsString))
            {
                _project.Tags = TagsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(tag => tag.Trim())
                                          .Where(tag => !string.IsNullOrEmpty(tag))
                                          .ToList();
            }
            else
            {
                _project.Tags = new List<string>();
            }

            var saveSuccess = await _projectService.SaveProjectAsync(_project);

            if (saveSuccess)
            {
                ProjectSaved?.Invoke(this, _project);
            }
        }
        catch (Exception ex)
        {
            // TODO: 显示错误消息
            System.Diagnostics.Debug.WriteLine($"保存项目失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogCancelled?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (_project != null)
        {
            try
            {
                // TODO: 显示确认对话框
                await _projectService.DeleteProjectAsync(_project.Id);
                ProjectDeleted?.Invoke(this, _project.Id);
            }
            catch (Exception ex)
            {
                // TODO: 显示错误消息
                System.Diagnostics.Debug.WriteLine($"删除项目失败: {ex.Message}");
            }
        }
    }
}
