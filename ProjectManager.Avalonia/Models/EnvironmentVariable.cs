using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectManager.Avalonia.Models
{
    public partial class EnvironmentVariable : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _value = string.Empty;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private string _description = string.Empty;

        public EnvironmentVariable()
        {
        }

        public EnvironmentVariable(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}
