using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.ViewModels.Pages;

namespace ProjectManager.Avalonia.Views.Pages;

public partial class SystemEnvironmentVariablesPage : UserControl
{
    public SystemEnvironmentVariablesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SystemEnvironmentVariablesViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateLayout(vm.SelectedFilterIndex);
            await vm.EnsureInitializedAsync();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemEnvironmentVariablesViewModel.SelectedFilterIndex))
        {
            if (DataContext is SystemEnvironmentVariablesViewModel vm)
                UpdateLayout(vm.SelectedFilterIndex);
        }
    }

    private void UpdateLayout(int filterIndex)
    {
        var sectionsGrid = this.FindControl<Grid>("SectionsGrid");
        var userSection = this.FindControl<Border>("UserSection");
        var sysSection = this.FindControl<Border>("SystemSection");

        if (sectionsGrid == null || userSection == null || sysSection == null)
            return;

        var cols = sectionsGrid.ColumnDefinitions;
        if (cols.Count < 3) return;

        switch (filterIndex)
        {
            case 1: // User only
                cols[0].Width = new GridLength(1, GridUnitType.Star);
                cols[1].Width = new GridLength(0);
                cols[2].Width = new GridLength(0);
                userSection.IsVisible = true;
                sysSection.IsVisible = false;
                break;
            case 2: // System only
                cols[0].Width = new GridLength(0);
                cols[1].Width = new GridLength(0);
                cols[2].Width = new GridLength(1, GridUnitType.Star);
                userSection.IsVisible = false;
                sysSection.IsVisible = true;
                break;
            default: // All
                cols[0].Width = new GridLength(1, GridUnitType.Star);
                cols[1].Width = new GridLength(12);
                cols[2].Width = new GridLength(1, GridUnitType.Star);
                userSection.IsVisible = true;
                sysSection.IsVisible = true;
                break;
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var searchBox = this.FindControl<TextBox>("SearchTextBox");
        if (searchBox == null || !searchBox.IsFocused) return;

        var pos = e.GetCurrentPoint(this).Position;
        var hit = this.InputHitTest(pos) as Control;

        Control? current = hit;
        while (current != null)
        {
            if (current == searchBox) return;
            current = current.Parent as Control;
        }

        var root = this.FindControl<Grid>("RootGrid");
        if (root != null)
        {
            root.Focusable = true;
            root.Focus();
            root.Focusable = false;
        }
    }

    private void OnDataGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is DataGrid dataGrid)
        {
            var point = e.GetCurrentPoint(dataGrid);
            var hit = dataGrid.InputHitTest(point.Position) as Control;
            Control? current = hit;
            bool foundRow = false;
            while (current != null && current != dataGrid)
            {
                if (current is DataGridRow)
                {
                    foundRow = true;
                    break;
                }
                current = current.Parent as Control;
            }

            if (!foundRow)
            {
                if (DataContext is SystemEnvironmentVariablesViewModel vm)
                {
                    if (dataGrid.Name == "UserDataGrid")
                        vm.SelectedUserVariable = null;
                    else if (dataGrid.Name == "SystemDataGrid")
                        vm.SelectedSystemVariable = null;
                }
            }
        }
    }

    private void OnDataGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid dataGrid && DataContext is SystemEnvironmentVariablesViewModel vm)
        {
            var point = e.GetPosition(dataGrid);
            var hit = dataGrid.InputHitTest(point) as Control;
            Control? current = hit;
            bool foundRow = false;
            while (current != null && current != dataGrid)
            {
                if (current is DataGridRow)
                {
                    foundRow = true;
                    break;
                }
                current = current.Parent as Control;
            }

            if (foundRow)
            {
                if (dataGrid.Name == "UserDataGrid" && vm.EditUserVariableCommand.CanExecute(null))
                    _ = vm.EditUserVariableCommand.ExecuteAsync(null);
                else if (dataGrid.Name == "SystemDataGrid" && vm.EditSystemVariableCommand.CanExecute(null))
                    _ = vm.EditSystemVariableCommand.ExecuteAsync(null);
            }
        }
    }
}
