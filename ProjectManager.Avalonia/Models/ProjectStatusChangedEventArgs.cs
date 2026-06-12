namespace ProjectManager.Avalonia.Models
{
    /// <summary>
    /// 项目状态变更事件参数
    /// </summary>
    public class ProjectStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 项目ID
        /// </summary>
        public string ProjectId { get; }

        /// <summary>
        /// 项目状态
        /// </summary>
        public ProjectStatus Status { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="projectId">项目ID</param>
        /// <param name="status">项目状态</param>
        public ProjectStatusChangedEventArgs(string projectId, ProjectStatus status)
        {
            ProjectId = projectId;
            Status = status;
        }
    }
}
