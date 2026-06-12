using System.Collections.Generic;

namespace ProjectManager.Avalonia.Models
{
    /// <summary>
    /// 框架配置模型，用于定义不同框架的默认设置
    /// </summary>
    public class FrameworkConfig
    {
        public string Name { get; set; } = string.Empty;
        public string DefaultStartCommand { get; set; } = string.Empty;
        public int DefaultPort { get; set; } = 0;
        public List<string> DefaultTags { get; set; } = new();
        public string Description { get; set; } = string.Empty;
        public string FileExtensions { get; set; } = string.Empty;
        public string RequirementsFile { get; set; } = string.Empty;
        public List<string> CommonCommands { get; set; } = new();
    }

    /// <summary>
    /// 框架配置服务，提供预定义的框架配置
    /// </summary>
    public static class FrameworkConfigService
    {
        public static readonly Dictionary<string, FrameworkConfig> Configs = new()
        {
            ["ComfyUI"] = new FrameworkConfig
            {
                Name = "ComfyUI",
                DefaultStartCommand = "python main.py",
                DefaultPort = 8188,
                DefaultTags = new List<string> { "AI绘画", "图像生成", "工作流", "节点编辑" },
                Description = "ComfyUI图像生成工作流",
                FileExtensions = "*.py,*.json",
                RequirementsFile = "requirements.txt",
                CommonCommands = new List<string> { "python main.py", "python main.py --listen", "python main.py --port 8188" }
            },
            
            ["Node.js"] = new FrameworkConfig
            {
                Name = "Node.js",
                DefaultStartCommand = "npm start",
                DefaultPort = 3000,
                DefaultTags = new List<string> { "JavaScript", "Node.js", "后端", "全栈" },
                Description = "Node.js JavaScript运行时",
                FileExtensions = "*.js,*.ts",
                RequirementsFile = "package.json",
                CommonCommands = new List<string> { "npm start", "node app.js", "npm run dev" }
            },
            
            [".NET"] = new FrameworkConfig
            {
                Name = ".NET",
                DefaultStartCommand = "dotnet run",
                DefaultPort = 5000,
                DefaultTags = new List<string> { ".NET", "C#", "后端", "Web API" },
                Description = ".NET应用程序",
                FileExtensions = "*.cs,*.csproj",
                RequirementsFile = "*.csproj",
                CommonCommands = new List<string> { "dotnet run", "dotnet build", "dotnet watch run" }
            },
            
            ["其他"] = new FrameworkConfig
            {
                Name = "其他",
                DefaultStartCommand = "python main.py",
                DefaultPort = 8080,
                DefaultTags = new List<string> { "自定义" },
                Description = "自定义项目类型",
                FileExtensions = "*.*",
                RequirementsFile = "requirements.txt",
                CommonCommands = new List<string> { "python main.py", "python app.py", "npm start" }
            }
        };

        /// <summary>
        /// 获取所有框架名称
        /// </summary>
        public static List<string> GetFrameworkNames()
        {
            return Configs.Keys.ToList();
        }

        /// <summary>
        /// 获取框架配置
        /// </summary>
        public static FrameworkConfig? GetFrameworkConfig(string frameworkName)
        {
            return Configs.TryGetValue(frameworkName, out var config) ? config : null;
        }
    }
}
