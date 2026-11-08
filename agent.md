# Desktop Agent 入口

先阅读本仓库的 [plan.md](plan.md)，再读取 [Core 共享 Agent 规则](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/agent.md)和[贡献指南](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/CONTRIBUTING.md)。这些规则适用于本仓库，公共内容只在 Core 维护。

同级克隆时可直接读取 `../ShitBotUltra/agent.md`、`../ShitBotUltra/CONTRIBUTING.md` 及其引用文件；无法读取时明确说明缺失，不自行编造规则。

本仓库负责 C# + WinUI 3 原生 Windows 界面，根命名空间为 `shitbotteam.ultra.desktop`。界面经管理 API 使用 Core；不直接写业务数据库，不复制业务逻辑，不以 WebView 取代默认界面。Avalonia 仅为已说明条件下的备选。当前仅做规划，README 后续再写。
