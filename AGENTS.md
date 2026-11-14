# Desktop 代理工作规范

本文件是本仓库直接执行入口。公共详细规则以 [Core AGENTS.md](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/AGENTS.md) 为准；本地 agent.md 只做导航。用户当前明确要求优先。

## 当前范围

- 本阶段只写计划、代理规范与许可证，不写功能代码，不生成工程骨架、依赖或构建配置；README 后续再写。
- 不运行测试、构建、应用或手动触发 CI，测试由维护者自行完成；可以检查文档差异、Git 状态和提交签名结果。
- 先确认当前仓库、分支和未提交修改，再从 [plan.md](plan.md) 按任务阅读；保留用户改动，不自行扩展工作范围。

## 本仓库边界

本仓库负责 C# + WinUI 3 + XAML 原生 Windows 界面，根命名空间为 shitbotteam.ultra.desktop。经管理 API 使用 Core，区分自己启动与附加的进程，不直写业务数据库，不复制机器人或业务逻辑。Avalonia 仅为既定条件下的备选。

宿主、配置与界面见公共计划 01 / 02 / 03；插件、图片模板及批量白名单导入通过 Core 接口展示。新版本提供提示、检查更新和升级按钮，安装资产按 SHA-256 校验，不设计发行包 sig。

## 公共规范与交付

同级克隆时读取 ../ShitBotUltra/AGENTS.md；没有 Core 克隆时读取上面的 GitHub 链接，按任务进入公共计划，不假设跨仓库规则自动加载。公共架构不在本仓库复制维护。

使用系统 Git 和已配置身份，按职责分批签名 commit 后 push；当前文档任务已获推送授权。不加助手署名或 AI Co-authored-by，不关闭提交签名，不重写历史；推送后确认 GitHub Verified，不能只报本地验签。

提交格式见 [贡献指南](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/CONTRIBUTING.md)，许可边界见 [许可说明](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/docs/licensing.md)。本仓库提供完整 [AGPL-3.0-only LICENSE](LICENSE)。最终如实说明文档范围、提交与推送状态、Verified 结果及未运行测试 / 构建。
