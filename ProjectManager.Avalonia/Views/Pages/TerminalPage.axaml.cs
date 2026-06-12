using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ProjectManager.Avalonia.ViewModels.Pages;

namespace ProjectManager.Avalonia.Views.Pages;

public partial class TerminalPage : UserControl
{
    public TerminalPage()
    {
        InitializeComponent();
    }

    private TerminalViewModel? ViewModel => DataContext as TerminalViewModel;

    private void ScrollToTop_Click(object? sender, RoutedEventArgs e)
    {
        var sv = FindTerminalScrollViewer();
        if (sv != null)
        {
            sv.Offset = new Vector(sv.Offset.X, 0);
        }
    }

    private void ScrollToBottom_Click(object? sender, RoutedEventArgs e)
    {
        var sv = FindTerminalScrollViewer();
        if (sv != null)
        {
            var maxOffset = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
            sv.Offset = new Vector(sv.Offset.X, maxOffset);
        }
    }

    private void CopyAll_Click(object? sender, RoutedEventArgs e)
    {
        var session = ViewModel?.SelectedSession;
        if (session == null) return;

        var sb = new StringBuilder();
        foreach (var line in session.OutputLines)
        {
            // Strip ANSI escape codes for clean clipboard text
            sb.AppendLine(StripAnsiCodes(line));
        }

        var text = sb.ToString();
        if (!string.IsNullOrEmpty(text))
        {
            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    _ = clipboard.SetTextAsync(text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Copy to clipboard failed: {ex.Message}");
            }
        }
    }

    private void ClearOutput_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ClearOutputCommand.Execute(null);
    }

    private void AutoWrapToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggleButton) return;
        var isWrapEnabled = toggleButton.IsChecked == true;

        var textBlock = FindTerminalOutputTextBlock();
        if (textBlock != null)
        {
            textBlock.TextWrapping = isWrapEnabled ? TextWrapping.Wrap : TextWrapping.NoWrap;
        }

        var scrollViewer = FindTerminalScrollViewer();
        if (scrollViewer != null)
        {
            scrollViewer.HorizontalScrollBarVisibility = isWrapEnabled
                ? ScrollBarVisibility.Disabled
                : ScrollBarVisibility.Auto;
        }
    }

    private ScrollViewer? FindTerminalScrollViewer()
    {
        // Search the visual tree for a ScrollViewer inside the TabControl content area
        return FindScrollViewerRecursive(this);
    }

    private SelectableTextBlock? FindTerminalOutputTextBlock()
    {
        return FindSelectableTextBlockRecursive(this);
    }

    private static SelectableTextBlock? FindSelectableTextBlockRecursive(Visual? visual)
    {
        if (visual == null) return null;
        if (visual is SelectableTextBlock stb) return stb;

        foreach (var child in visual.GetVisualChildren())
        {
            if (child is Visual childVisual)
            {
                var result = FindSelectableTextBlockRecursive(childVisual);
                if (result != null) return result;
            }
        }
        return null;
    }

    private static ScrollViewer? FindScrollViewerRecursive(Visual? visual)
    {
        if (visual == null) return null;
        if (visual is ScrollViewer sv) return sv;

        foreach (var child in visual.GetVisualChildren())
        {
            if (child is Visual childVisual)
            {
                var result = FindScrollViewerRecursive(childVisual);
                if (result != null) return result;
            }
        }
        return null;
    }

    /// <summary>
    /// Strips ANSI escape codes from a string for clean text output.
    /// </summary>
    private static string StripAnsiCodes(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return System.Text.RegularExpressions.Regex.Replace(text, @"\u001B\[[0-9;]*[a-zA-Z]", "");
    }
}
