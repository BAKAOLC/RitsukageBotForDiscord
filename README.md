# Ritsukage Bot For Discord

一个功能丰富的 Discord 机器人，支持 AI 聊天、B站内容获取、GitHub 集成、图像处理等多种功能。

## ✨ 主要功能

### 🤖 AI 智能对话
- 支持多种 AI 服务（OpenAI、Ollama 等）
- 可配置的角色扮演模式
- 智能预处理和后处理功能
- 灵活的提示词配置系统

### 📺 Bilibili 集成
- 视频信息查询和分享
- 用户动态获取
- 直播状态监控
- 视频评论查看
- 用户信息展示

### 🔧 GitHub 集成
- 仓库信息查询
- Issue 和 PR 状态监控
- 代码仓库统计
- 发布信息获取

### 🖼️ 图像处理
- 图像生成和编辑
- 多种图像效果处理
- 自定义图像命令

### ⛏️ Minecraft 相关
- 服务器状态查询
- 玩家信息获取
- Minecraft 相关工具

### 🕒 时间和调度
- 自动时间广播
- 定时任务管理
- 游戏状态更新

### 🍬 互动功能
- 糖果系统（用户可以给机器人糖果）
- 用户配置管理
- 机器人信息查询

## 🛠️ 技术栈

- **.NET 9.0** - 主要开发框架
- **Discord.Net** - Discord API 集成
- **SQLite** - 数据存储和缓存
- **MediatR** - CQRS 模式实现
- **NLog** - 日志记录
- **SixLabors.ImageSharp** - 图像处理
- **Newtonsoft.Json** - JSON 序列化
- **BiliKernel** - Bilibili API 集成
- **Octokit** - GitHub API 集成
- **Google APIs** - 搜索功能集成

## 📋 系统要求

- .NET 9.0 运行时
- 支持 Windows、Linux、macOS
- 至少 512MB 内存
- 网络连接（用于 Discord、API 调用等）

## 🚀 快速开始

### 1. 下载和安装

#### 从 Release 下载（推荐）
1. 前往 [Releases](https://github.com/BAKAOLC/RitsukageBotForDiscord/releases) 页面
2. 下载适合您系统的最新版本
3. 解压到目标目录

#### 从源代码构建
```bash
# 克隆仓库
git clone https://github.com/BAKAOLC/RitsukageBotForDiscord.git
cd RitsukageBotForDiscord

# 安装依赖（需要 .NET 9.0 SDK）
dotnet restore

# 构建项目
dotnet build --configuration Release

# 运行
dotnet run --project src/RitsukageBotForDiscord/RitsukageBotForDiscord.csproj
```

### 2. 配置

#### 基础配置
复制 `appsettings.json` 并根据需要修改：

```json
{
  "Discord": {
    "Token": "你的Discord机器人Token"
  },
  "GitHub": {
    "ProductHeader": "RitsukageBotForDiscord",
    "AppClientId": "GitHub应用客户端ID"
  },
  "Google": {
    "ApiKey": "Google API密钥",
    "SearchEngineId": "搜索引擎ID"
  },
  "Cache": "cache.db",
  "Sqlite": "data.db"
}
```

#### Discord 机器人设置
1. 前往 [Discord Developer Portal](https://discord.com/developers/applications)
2. 创建新应用程序
3. 在 "Bot" 选项卡中创建机器人并获取 Token
4. 设置必要的权限：
   - Send Messages
   - Use Slash Commands
   - Read Message History
   - Add Reactions
   - Attach Files
   - Embed Links

#### AI 功能配置（可选）
```json
{
  "AI": {
    "Enabled": true,
    "Service": [
      {
        "Name": "OpenAI",
        "Endpoint": "https://api.openai.com/v1",
        "ApiKey": "your-openai-api-key",
        "ModelId": "gpt-3.5-turbo"
      }
    ]
  }
}
```

### 3. 运行机器人

```bash
# 直接运行
dotnet run --project src/RitsukageBotForDiscord/RitsukageBotForDiscord.csproj

# 或者运行编译后的程序
./bin/Release/net9.0/RitsukageBot
```

## 📖 使用指南

### 斜杠命令

机器人支持以下主要命令：

- `/bot info` - 查看机器人信息
- `/candy` - 给机器人一颗糖果
- `/time` - 时间相关功能
- `/github` - GitHub 仓库查询
- `/bilibili` - B站内容查询
- `/ai` - AI 聊天功能（需要配置）
- `/minecraft` - Minecraft 相关功能

### 配置说明

#### 自动更新设置
```json
{
  "AutoUpdate": {
    "Enabled": true,
    "CheckInterval": 3600000,
    "Information": {
      "RepositoryOwner": "BAKAOLC",
      "RepositoryName": "RitsukageBotForDiscord",
      "BranchName": "main",
      "TargetJobName": "Build - {0} - 9.0.x"
    }
  }
}
```

#### 自定义游戏状态
```json
{
  "CustomValues": {
    "GameStatus": [
      {
        "Type": "Playing",
        "Name": "Minecraft"
      }
    ]
  }
}
```

## 🔧 开发指南

### 开发环境设置

1. 安装 .NET 9.0 SDK
2. 推荐使用 Visual Studio 2022 或 JetBrains Rider
3. 安装 Git 版本控制

### 项目结构

```
src/RitsukageBotForDiscord/
├── Modules/              # 功能模块
│   ├── AI/              # AI 相关功能
│   ├── Bilibili/        # B站集成
│   ├── Github/          # GitHub 集成
│   └── ...
├── Services/            # 服务层
├── Library/             # 公共库
├── Options/             # 配置选项
├── Program.cs           # 程序入口
└── appsettings.json     # 配置文件
```

### 添加新功能模块

1. 在 `Modules` 目录下创建新的模块类
2. 继承 `InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>`
3. 使用 `[SlashCommand]` 特性定义命令
4. 在服务容器中注册需要的依赖

示例：
```csharp
[Group("example", "Example Commands")]
public class ExampleInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
{
    [SlashCommand("hello", "Say hello")]
    public async Task HelloAsync()
    {
        await RespondAsync("Hello World!").ConfigureAwait(false);
    }
}
```

### 构建和测试

```bash
# 恢复依赖
dotnet restore

# 构建项目
dotnet build

# 运行测试（如果有）
dotnet test

# 发布版本
dotnet publish -c Release -r win-x64 --self-contained
```

## 🤝 贡献指南

我们欢迎社区贡献！

### 如何贡献

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

### 代码规范

- 遵循 C# 编码规范
- 使用有意义的变量和方法名
- 添加适当的注释和文档
- 确保代码通过现有测试

### 问题反馈

如果您发现任何问题或有功能建议，请：

1. 检查 [Issues](https://github.com/BAKAOLC/RitsukageBotForDiscord/issues) 是否已存在相关问题
2. 如果没有，请创建新的 Issue 并提供详细描述
3. 包含错误重现步骤（如果适用）

## 📄 许可证

本项目采用 [GNU General Public License v3.0](LICENSE) 许可证。

## 🙏 致谢

感谢以下开源项目和贡献者：

- [Discord.Net](https://github.com/discord-net/Discord.Net) - Discord API 集成
- [BiliKernel](https://github.com/Richasy/BiliKernel) - Bilibili API 集成
- [Octokit](https://github.com/octokit/octokit.net) - GitHub API 集成
- 所有贡献者和社区成员

## 📞 联系方式

- GitHub Issues: [问题反馈](https://github.com/BAKAOLC/RitsukageBotForDiscord/issues)
- 项目主页: [RitsukageBotForDiscord](https://github.com/BAKAOLC/RitsukageBotForDiscord)

---

**注意**: 使用本机器人需要遵循 Discord 服务条款以及相关 API 的使用条款。请确保您有权使用配置的各种 API 服务。