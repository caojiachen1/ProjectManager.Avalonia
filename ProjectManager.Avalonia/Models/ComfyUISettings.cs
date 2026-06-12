namespace ProjectManager.Avalonia.Models;

/// <summary>
/// ComfyUI 专用的可持久化设置（避免仅依赖 StartCommand 解析）。
/// </summary>
public class ComfyUISettings
{
    // 基础设置
    public string ListenAddress { get; set; } = "127.0.0.1";
    public bool ListenAllInterfaces { get; set; }
    public int Port { get; set; } = 8188;
    public string TlsKeyFile { get; set; } = string.Empty;
    public string TlsCertFile { get; set; } = string.Empty;
    public bool EnableCorsHeader { get; set; }
    public string CorsOrigin { get; set; } = "*";
    public float MaxUploadSize { get; set; } = 100;

    // Python 设置（启动脚本固定为 ComfyUI 根目录下的 main.py，不再可配置）

    // 互斥选项组（下拉菜单）
    public MemoryManagementMode MemoryManagementMode { get; set; } = MemoryManagementMode.None;
    public UNetPrecisionMode UNetPrecisionMode { get; set; } = UNetPrecisionMode.None;
    public VAEPrecisionMode VAEPrecisionMode { get; set; } = VAEPrecisionMode.None;
    public AttentionAlgorithmMode AttentionAlgorithmMode { get; set; } = AttentionAlgorithmMode.None;
    public CacheMode CacheMode { get; set; } = CacheMode.Default;
    public TextEncoderPrecisionMode TextEncoderPrecisionMode { get; set; } = TextEncoderPrecisionMode.None;
    public GlobalPrecisionForceMode GlobalPrecisionForceMode { get; set; } = GlobalPrecisionForceMode.None;
    public CudaMemoryAllocatorMode CudaMemoryAllocatorMode { get; set; } = CudaMemoryAllocatorMode.Default;
    public AttentionUpcastMode AttentionUpcastMode { get; set; } = AttentionUpcastMode.Default;
    public BrowserAutoLaunchMode BrowserAutoLaunchMode { get; set; } = BrowserAutoLaunchMode.Default;

    // 目录设置
    public string BaseDirectory { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public string TempDirectory { get; set; } = string.Empty;
    public string InputDirectory { get; set; } = string.Empty;
    public string UserDirectory { get; set; } = string.Empty;
    public string ExtraModelPathsConfig { get; set; } = string.Empty;

    // 启动设置
    public bool AutoLaunch { get; set; }
    public bool DisableAutoLaunch { get; set; }

    // GPU/CUDA设置
    public int? CudaDevice { get; set; }
    public int? DefaultDevice { get; set; }
    public bool CudaMalloc { get; set; }
    public bool DisableCudaMalloc { get; set; }
    public int? DirectmlDevice { get; set; }
    public string OneApiDeviceSelector { get; set; } = string.Empty;
    public bool DisableIpexOptimize { get; set; }
    public bool SupportsFp8Compute { get; set; }

    // 精度设置
    public bool ForceFp32 { get; set; }
    public bool ForceFp16 { get; set; }
    public bool Fp32Unet { get; set; }
    public bool Fp64Unet { get; set; }
    public bool Bf16Unet { get; set; }
    public bool Fp16Unet { get; set; }
    public bool Fp8E4M3FnUnet { get; set; }
    public bool Fp8E5M2Unet { get; set; }
    public bool Fp8E8M0FnuUnet { get; set; }
    public bool Fp16Vae { get; set; }
    public bool Fp32Vae { get; set; }
    public bool Bf16Vae { get; set; }
    public bool CpuVae { get; set; }
    public bool Fp8E4M3FnTextEnc { get; set; }
    public bool Fp8E5M2TextEnc { get; set; }
    public bool Fp16TextEnc { get; set; }
    public bool Fp32TextEnc { get; set; }
    public bool Bf16TextEnc { get; set; }
    public bool ForceChannelsLast { get; set; }

    // 内存管理
    public bool GpuOnly { get; set; }
    public bool HighVram { get; set; }
    public bool NormalVram { get; set; }
    public bool LowVramMode { get; set; }
    public bool NoVram { get; set; }
    public bool CpuMode { get; set; }
    public float? ReserveVram { get; set; }
    public bool AsyncOffload { get; set; }
    public bool ForceNonBlocking { get; set; }
    public bool DisableSmartMemory { get; set; }

    // 预览设置
    public string PreviewMethod { get; set; } = "none";
    public int PreviewSize { get; set; } = 512;

    // 缓存设置
    public bool CacheClassic { get; set; }
    public int CacheLru { get; set; } = 0;
    public bool CacheNone { get; set; }

    // 注意力机制设置
    public bool UseSplitCrossAttention { get; set; }
    public bool UseQuadCrossAttention { get; set; }
    public bool UsePytorchCrossAttention { get; set; }
    public bool UseSageAttention { get; set; }
    public bool UseFlashAttention { get; set; }
    public bool DisableXformers { get; set; }
    public bool ForceUpcastAttention { get; set; }
    public bool DontUpcastAttention { get; set; }

    // 性能设置
    public bool Deterministic { get; set; }
    public bool FastFp16Accumulation { get; set; }
    public bool FastFp8MatrixMult { get; set; }
    public bool FastCublasOps { get; set; }
    public bool MmapTorchFiles { get; set; }
    public bool DisableMmap { get; set; }

    // 哈希设置
    public string DefaultHashingFunction { get; set; } = "sha256";

    // 调试和日志设置
    public bool DontPrintServer { get; set; }
    public bool QuickTestForCi { get; set; }
    public bool WindowsStandaloneBuild { get; set; }
    public string Verbose { get; set; } = "INFO";
    public bool LogStdout { get; set; }

    // 元数据和自定义节点
    public bool DisableMetadata { get; set; }
    public bool DisableAllCustomNodes { get; set; }
    public string WhitelistCustomNodes { get; set; } = string.Empty;
    public bool DisableApiNodes { get; set; }

    // 多用户设置
    public bool MultiUser { get; set; }

    // 前端设置
    public string FrontEndVersion { get; set; } = "comfyanonymous/ComfyUI@latest";
    public string FrontEndRoot { get; set; } = string.Empty;
    public bool EnableCompressResponseBody { get; set; }
    public string ComfyApiBase { get; set; } = "https://api.comfy.org";
    public string DatabaseUrl { get; set; } = string.Empty;

    // 路径设置（保留原有设置）
    /// <summary>
    /// ComfyUI 根目录路径（包含 main.py 与 custom_nodes 的目录）。
    /// 启动脚本固定为该目录下的 main.py，已移除单独的"启动脚本文件"设置。
    /// 为空时表示未显式指定，将根据项目本地路径进行自动推断。
   /// </summary>
    public string ComfyUIRootPath { get; set; } = string.Empty;

    public string PythonPath { get; set; } = string.Empty;
    public string ModelsPath { get; set; } = "./models";
    public string OutputPath { get; set; } = "./output";
    public string ExtraArgs { get; set; } = string.Empty;
    public string CustomNodesPath { get; set; } = "./custom_nodes";
    public bool AutoLoadWorkflow { get; set; } = true;
    public bool EnableWorkflowSnapshots { get; set; } = false;
}
