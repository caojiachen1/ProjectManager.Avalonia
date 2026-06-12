using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Converters;

/// <summary>
/// ComfyUI 内存管理模式枚举转换器
/// </summary>
public class MemoryManagementModeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MemoryManagementMode mode)
        {
            return mode switch
            {
                MemoryManagementMode.None => "不设置（默认）",
                MemoryManagementMode.GpuOnly => "仅使用GPU (--gpu-only)",
                MemoryManagementMode.HighVram => "高显存模式 (--highvram)",
                MemoryManagementMode.NormalVram => "标准显存模式 (--normalvram)",
                MemoryManagementMode.LowVram => "低显存模式 (--lowvram)",
                MemoryManagementMode.NoVram => "无显存模式 (--novram)",
                MemoryManagementMode.CpuMode => "使用CPU运行 (--cpu)",
                _ => "未知"
            };
        }
        return "不设置（默认）";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str switch
            {
                "不设置（默认）" => MemoryManagementMode.None,
                "仅使用GPU (--gpu-only)" => MemoryManagementMode.GpuOnly,
                "高显存模式 (--highvram)" => MemoryManagementMode.HighVram,
                "标准显存模式 (--normalvram)" => MemoryManagementMode.NormalVram,
                "低显存模式 (--lowvram)" => MemoryManagementMode.LowVram,
                "无显存模式 (--novram)" => MemoryManagementMode.NoVram,
                "使用CPU运行 (--cpu)" => MemoryManagementMode.CpuMode,
                _ => MemoryManagementMode.None
            };
        }
        return MemoryManagementMode.None;
    }
}

/// <summary>
/// ComfyUI UNet精度模式枚举转换器
/// </summary>
public class UNetPrecisionModeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is UNetPrecisionMode mode)
        {
            return mode switch
            {
                UNetPrecisionMode.None => "不设置（默认）",
                UNetPrecisionMode.FP32 => "FP32 UNet (--fp32-unet)",
                UNetPrecisionMode.FP64 => "FP64 UNet (--fp64-unet)",
                UNetPrecisionMode.BF16 => "BF16 UNet (--bf16-unet)",
                UNetPrecisionMode.FP16 => "FP16 UNet (--fp16-unet)",
                UNetPrecisionMode.FP8_E4M3FN => "FP8 E4M3FN UNet (--fp8_e4m3fn-unet)",
                UNetPrecisionMode.FP8_E5M2 => "FP8 E5M2 UNet (--fp8_e5m2-unet)",
                UNetPrecisionMode.FP8_E8M0FNU => "FP8 E8M0FNU UNet (--fp8_e8m0fnu-unet)",
                _ => "未知"
            };
        }
        return "不设置（默认）";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str switch
            {
                "不设置（默认）" => UNetPrecisionMode.None,
                "FP32 UNet (--fp32-unet)" => UNetPrecisionMode.FP32,
                "FP64 UNet (--fp64-unet)" => UNetPrecisionMode.FP64,
                "BF16 UNet (--bf16-unet)" => UNetPrecisionMode.BF16,
                "FP16 UNet (--fp16-unet)" => UNetPrecisionMode.FP16,
                "FP8 E4M3FN UNet (--fp8_e4m3fn-unet)" => UNetPrecisionMode.FP8_E4M3FN,
                "FP8 E5M2 UNet (--fp8_e5m2-unet)" => UNetPrecisionMode.FP8_E5M2,
                "FP8 E8M0FNU UNet (--fp8_e8m0fnu-unet)" => UNetPrecisionMode.FP8_E8M0FNU,
                _ => UNetPrecisionMode.None
            };
        }
        return UNetPrecisionMode.None;
    }
}

/// <summary>
/// ComfyUI VAE精度模式枚举转换器
/// </summary>
public class VAEPrecisionModeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is VAEPrecisionMode mode)
        {
            return mode switch
            {
                VAEPrecisionMode.None => "不设置（默认）",
                VAEPrecisionMode.FP16 => "FP16 VAE (--fp16-vae)",
                VAEPrecisionMode.FP32 => "FP32 VAE (--fp32-vae)",
                VAEPrecisionMode.BF16 => "BF16 VAE (--bf16-vae)",
                VAEPrecisionMode.CPU => "CPU VAE (--cpu-vae)",
                _ => "未知"
            };
        }
        return "不设置（默认）";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str switch
            {
                "不设置（默认）" => VAEPrecisionMode.None,
                "FP16 VAE (--fp16-vae)" => VAEPrecisionMode.FP16,
                "FP32 VAE (--fp32-vae)" => VAEPrecisionMode.FP32,
                "BF16 VAE (--bf16-vae)" => VAEPrecisionMode.BF16,
                "CPU VAE (--cpu-vae)" => VAEPrecisionMode.CPU,
                _ => VAEPrecisionMode.None
            };
        }
        return VAEPrecisionMode.None;
    }
}

/// <summary>
/// ComfyUI 注意力算法模式枚举转换器
/// </summary>
public class AttentionAlgorithmModeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AttentionAlgorithmMode mode)
        {
            return mode switch
            {
                AttentionAlgorithmMode.None => "不设置（默认）",
                AttentionAlgorithmMode.SplitCrossAttention => "分割交叉注意力 (--use-split-cross-attention)",
                AttentionAlgorithmMode.QuadCrossAttention => "四元交叉注意力 (--use-quad-cross-attention)",
                AttentionAlgorithmMode.PytorchCrossAttention => "PyTorch交叉注意力 (--use-pytorch-cross-attention)",
                AttentionAlgorithmMode.SageAttention => "Sage注意力 (--use-sage-attention)",
                AttentionAlgorithmMode.FlashAttention => "Flash注意力 (--use-flash-attention)",
                _ => "未知"
            };
        }
        return "不设置（默认）";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str switch
            {
                "不设置（默认）" => AttentionAlgorithmMode.None,
                "分割交叉注意力 (--use-split-cross-attention)" => AttentionAlgorithmMode.SplitCrossAttention,
                "四元交叉注意力 (--use-quad-cross-attention)" => AttentionAlgorithmMode.QuadCrossAttention,
                "PyTorch交叉注意力 (--use-pytorch-cross-attention)" => AttentionAlgorithmMode.PytorchCrossAttention,
                "Sage注意力 (--use-sage-attention)" => AttentionAlgorithmMode.SageAttention,
                "Flash注意力 (--use-flash-attention)" => AttentionAlgorithmMode.FlashAttention,
                _ => AttentionAlgorithmMode.None
            };
        }
        return AttentionAlgorithmMode.None;
    }
}

/// <summary>
/// ComfyUI 缓存模式枚举转换器
/// </summary>
public class CacheModeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CacheMode mode)
        {
            return mode switch
            {
                CacheMode.Default => "默认缓存模式",
                CacheMode.Classic => "经典缓存 (--cache-classic)",
                CacheMode.None => "无缓存 (--cache-none)",
                CacheMode.LRU => "LRU缓存 (--cache-lru)",
                _ => "未知"
            };
        }
        return "默认缓存模式";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str switch
            {
                "默认缓存模式" => CacheMode.Default,
                "经典缓存 (--cache-classic)" => CacheMode.Classic,
                "无缓存 (--cache-none)" => CacheMode.None,
                "LRU缓存 (--cache-lru)" => CacheMode.LRU,
                _ => CacheMode.Default
            };
        }
        return CacheMode.Default;
    }
}
