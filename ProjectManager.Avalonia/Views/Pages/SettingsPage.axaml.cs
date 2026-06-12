using Avalonia.Controls;
using ProjectManager.Avalonia.Services;
using ProjectManager.Avalonia.ViewModels.Pages;

namespace ProjectManager.Avalonia.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    /// <summary>
    /// Guard flag to suppress SelectionChanged side effects while InitializeViewModelAsync
    /// is assigning SelectedLanguage / SelectedLanguageInfo. Without this, the handler
    /// would fire during initialization and overwrite the saved language.
    /// </summary>
    private bool _suppressLanguageSelectionChanged;

    private void LanguageComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageSelectionChanged) return;
        if (ViewModel == null) return;
        if (!ViewModel.IsInitialized) return;
        if (sender is not ComboBox comboBox) return;
        if (comboBox.SelectedItem is not LanguageInfo info) return;
        if (ViewModel.SelectedLanguage == info.Code) return;

        ViewModel.SelectedLanguage = info.Code;
    }

    /// <summary>
    /// Called by the ViewModel before/after it programmatically updates SelectedLanguageInfo
    /// during initialization, so the SelectionChanged handler does not race with the loader.
    /// </summary>
    public void SetSuppressLanguageSelection(bool suppress)
        => _suppressLanguageSelectionChanged = suppress;
}
