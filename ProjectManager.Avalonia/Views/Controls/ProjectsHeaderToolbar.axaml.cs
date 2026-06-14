using Avalonia.Controls;
using Avalonia.Threading;

namespace ProjectManager.Avalonia.Views.Controls;

public partial class ProjectsHeaderToolbar : UserControl
{
    public ProjectsHeaderToolbar()
    {
        InitializeComponent();
        // 确保 ComboBox 在 DataContext 变化后正确选中第一项
        DataContextChanged += (s, e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var cb = this.FindControl<ComboBox>("StatusFilterComboBox");
                if (cb != null && cb.Items.Count > 0 && cb.SelectedIndex < 0)
                    cb.SelectedIndex = 0;
            }, DispatcherPriority.Loaded);
        };
    }
}
