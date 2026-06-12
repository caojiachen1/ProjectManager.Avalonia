using System.Collections.ObjectModel;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class EnvironmentVariablesDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private Project? _project;

    [ObservableProperty]
    private ObservableCollection<EnvironmentVariable> _environmentVariables = new();

    [ObservableProperty]
    private EnvironmentVariable? _selectedVariable;

    [ObservableProperty]
    private string _newVariableName = string.Empty;

    [ObservableProperty]
    private string _newVariableValue = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<EnvironmentVariable> _filteredVariables = new();

    public EnvironmentVariablesDialogViewModel()
    {
        RefreshFilter();
    }

    public void LoadProject(Project project)
    {
        Project = project;
        EnvironmentVariables.Clear();

        foreach (var kvp in project.EnvironmentVariables)
        {
            EnvironmentVariables.Add(new EnvironmentVariable
            {
                Name = kvp.Key,
                Value = kvp.Value,
                IsEnabled = true
            });
        }
        RefreshFilter();
    }

    partial void OnSearchTextChanged(string value) => RefreshFilter();

    private void RefreshFilter()
    {
        var filtered = EnvironmentVariables.Where(FilterVariable).ToList();
        FilteredVariables.Clear();
        foreach (var v in filtered) FilteredVariables.Add(v);
    }

    private bool FilterVariable(EnvironmentVariable variable)
    {
        if (!string.IsNullOrEmpty(SearchText))
        {
            return variable.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   variable.Value.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }
        return true;
    }

    [RelayCommand]
    private void AddVariable()
    {
        if (string.IsNullOrWhiteSpace(NewVariableName)) return;
        if (EnvironmentVariables.Any(v => v.Name.Equals(NewVariableName, StringComparison.OrdinalIgnoreCase)))
            return;

        EnvironmentVariables.Add(new EnvironmentVariable
        {
            Name = NewVariableName.Trim(),
            Value = NewVariableValue?.Trim() ?? string.Empty,
            IsEnabled = true
        });

        NewVariableName = string.Empty;
        NewVariableValue = string.Empty;
        RefreshFilter();
    }

    [RelayCommand]
    private void RemoveVariable(EnvironmentVariable? variable)
    {
        if (variable != null)
        {
            EnvironmentVariables.Remove(variable);
            RefreshFilter();
        }
    }

    [RelayCommand]
    private void RemoveSelectedVariable()
    {
        if (SelectedVariable != null)
        {
            EnvironmentVariables.Remove(SelectedVariable);
            SelectedVariable = null;
            RefreshFilter();
        }
    }

    [RelayCommand]
    private void ClearAllVariables()
    {
        EnvironmentVariables.Clear();
        RefreshFilter();
    }

    public void SaveChanges()
    {
        if (Project == null) return;

        Project.EnvironmentVariables.Clear();
        foreach (var variable in EnvironmentVariables.Where(v => v.IsEnabled))
        {
            if (!string.IsNullOrWhiteSpace(variable.Name))
                Project.EnvironmentVariables[variable.Name] = variable.Value ?? string.Empty;
        }
    }
}
