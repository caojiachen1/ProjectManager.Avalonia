using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class PathTextEditViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _pathText = string.Empty;

    public event EventHandler<bool>? CloseRequested;

    public PathTextEditViewModel(string pathText)
    {
        _pathText = pathText;
    }

    public PathTextEditViewModel() : this("") { }

    [RelayCommand]
    private void Save()
    {
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, false);
    }
}
