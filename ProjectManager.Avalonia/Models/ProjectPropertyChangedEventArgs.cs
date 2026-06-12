using System;

namespace ProjectManager.Avalonia.Models
{
    public class ProjectPropertyChangedEventArgs : EventArgs
    {
        public ProjectPropertyChangedEventArgs(Project project, string propertyName)
        {
            Project = project;
            PropertyName = propertyName;
        }

        public Project Project { get; }

        public string PropertyName { get; }
    }
}
