using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.Views.Dialogs;

public partial class ComfyUIPluginsManagerWindow : Window
{
    public ComfyUIPluginsManagerWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is ComfyUIPluginsManagerViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.CustomNodesPath))
            {
                vm.StartLoadFromCustomNodes(vm.CustomNodesPath);
            }
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
