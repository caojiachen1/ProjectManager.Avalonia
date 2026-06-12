using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.Views.Dialogs;

public partial class ComfyUIProjectSettingsWindow : Window
{
    public ComfyUIProjectSettingsWindow()
    {
        InitializeComponent();
        PopulateEnumComboBoxes();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is ComfyUIProjectSettingsViewModel vm)
        {
            vm.ProjectSaved += (_, _) => Close(true);
            vm.ProjectDeleted += (_, _) => Close(true);
            vm.DialogCancelled += (_, _) => Close(false);
        }
    }

    private void PopulateEnumComboBoxes()
    {
        // Enum ComboBoxes
        SetEnumItems<MemoryManagementMode>(this.FindControl<ComboBox>("MemoryModeCombo"));
        SetEnumItems<UNetPrecisionMode>(this.FindControl<ComboBox>("UNetPrecisionCombo"));
        SetEnumItems<VAEPrecisionMode>(this.FindControl<ComboBox>("VAEPrecisionCombo"));
        SetEnumItems<TextEncoderPrecisionMode>(this.FindControl<ComboBox>("TextEncPrecisionCombo"));
        SetEnumItems<GlobalPrecisionForceMode>(this.FindControl<ComboBox>("GlobalPrecisionCombo"));
        SetEnumItems<AttentionAlgorithmMode>(this.FindControl<ComboBox>("AttentionAlgoCombo"));
        SetEnumItems<AttentionUpcastMode>(this.FindControl<ComboBox>("AttentionUpcastCombo"));
        SetEnumItems<CudaMemoryAllocatorMode>(this.FindControl<ComboBox>("CudaAllocatorCombo"));
        SetEnumItems<BrowserAutoLaunchMode>(this.FindControl<ComboBox>("BrowserLaunchModeCombo"));
        SetEnumItems<CacheMode>(this.FindControl<ComboBox>("CacheModeCombo"));

        // String-based ComboBoxes
        SetStringItems(this.FindControl<ComboBox>("PreviewMethodCombo"),
            new[] { "none", "auto", "latent2rgb", "taesd" });

        SetStringItems(this.FindControl<ComboBox>("VerboseCombo"),
            new[] { "DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL" });

        SetStringItems(this.FindControl<ComboBox>("HashingFuncCombo"),
            new[] { "md5", "sha1", "sha256", "sha512" });
    }

    private static void SetEnumItems<T>(ComboBox? comboBox) where T : Enum
    {
        if (comboBox != null)
        {
            comboBox.ItemsSource = Enum.GetValues(typeof(T));
        }
    }

    private static void SetStringItems(ComboBox? comboBox, string[] items)
    {
        if (comboBox != null)
        {
            comboBox.ItemsSource = items;
        }
    }
}
