namespace ProjectManager.Avalonia.Models
{
    /// <summary>
    /// Git仓库信息类，用于在Git管理界面显示仓库选择
    /// </summary>
    public class GitRepositoryInfo
    {
        /// <summary>
        /// 仓库路径
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称（通常是文件夹名称）
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 相对于项目根目录的路径
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// 是否是主仓库（项目根目录的仓库）
        /// </summary>
        public bool IsMainRepository { get; set; }

        public GitRepositoryInfo(string path, string projectRootPath)
        {
            Path = path;
            DisplayName = System.IO.Path.GetFileName(path);
            
            // 计算相对路径
            if (path.StartsWith(projectRootPath))
            {
                RelativePath = path.Substring(projectRootPath.Length).TrimStart(System.IO.Path.DirectorySeparatorChar);
                if (string.IsNullOrEmpty(RelativePath))
                {
                    RelativePath = "(根目录)";
                    IsMainRepository = true;
                    DisplayName = $"{DisplayName} (主仓库)";
                }
                else
                {
                    DisplayName = $"{DisplayName} ({RelativePath})";
                }
            }
        }
    }
}
