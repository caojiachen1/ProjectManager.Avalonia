using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectManager.Avalonia.Models
{
    public partial class SystemEnvironmentVariable : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _value = string.Empty;

        [ObservableProperty]
        private bool _isSystemVariable;

        [ObservableProperty]
        private bool _isExpanded;

        public SystemEnvironmentVariable()
        {
        }

        public SystemEnvironmentVariable(string name, string value, bool isSystemVariable)
        {
            Name = name;
            Value = value;
            IsSystemVariable = isSystemVariable;
        }

        public string DisplayName => $"{Name} {(IsSystemVariable ? "(系统)" : "(用户)")}";
    }
}
