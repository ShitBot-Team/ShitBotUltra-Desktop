# ShitBotUltra Desktop 计划

状态：仓库已建立，当前只包含计划、协作入口和许可证，尚未实现桌面程序。

## 定位与语言

默认采用 **C# + WinUI 3 + XAML**，提供 Windows 原生桌面界面；使用 CommunityToolkit.Mvvm 分离界面、状态和服务。根命名空间为 `shitbotteam.ultra.desktop`。

Desktop 通过 Core 管理 API 提供配置、连接状态、日志和生命周期操作。若跨平台需求或实际部署限制使 WinUI 不合适，再评估 C# + Avalonia；它是备选，不与 WinUI 同时实现两套界面。框架依据与部署取舍见 [Core 参考文档](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/docs/plan/05-参考项目与依据.md)。

## 宿主与配置行为

- 发行包携带固定版本及校验信息的 `sbu.exe`，通过 `serve --no-webui` 托管 Core，也可附加已有 Core 实例。
- 默认使用 XAML 界面，管理 API 保持可用；用户需要时可通过 Core 开启 WebUI，在浏览器中调试和管理。
- 区分自己启动的进程与附加的进程。关闭窗口、退出应用和停止服务分别处理，不随意终止已有实例。
- 可以直接打开配置目录，提供表单和配置原文编辑。在线时通过 Core 的版本校验与配置事务保存；离线时按公共文档获取排他锁、只读校验并原子保存。
- 配置文件可由 Desktop 管理，业务数据库仍由 Core 独占；界面层不另建机器人连接、绑定规则或业务存储。

这是拟定行为，具体接口、锁和失败处理统一以 Core 文档为准。

## 本仓库任务

1. 确定 Windows / .NET / Windows App SDK 版本、运行时依赖及打包方式，记录部署条件。
2. 建立 Views、ViewModels、Services 的界面结构，接入 Core 管理 API、状态与日志事件。
3. 实现启动 / 附加 / 停止宿主，以及打开目录、配置原文和表单编辑；遵守实例所有权与配置冲突规则。
4. 补充托盘、日志批量更新与虚拟化、错误提示和按需开启 WebUI；发行时固定 CLI 及管理 API 兼容范围。

程序目录树和配置事务不在此重复维护，分别引用 Core 的结构与协议文档。原生 UI 的性能、输入、DPI 和无障碍表现留给后续实际实现与维护者确认。

## 公共文档引用

- [总体规划](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/docs/plan/01-总体规划.md)：桌面选择、进程关系与实施阶段。
- [仓库结构与开发规范](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/docs/plan/02-仓库结构与开发规范.md)：Desktop 目录、命名空间、依赖与发行资源。
- [协议配置与迁移](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/docs/plan/03-协议配置与迁移.md)：管理 API、配置事务、进程所有权与离线编辑。
- [思维导图](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/docs/plan/04-思维导图.md)：四组件关系。
- [共享 Agent 规则](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/agent.md)与[贡献指南](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/CONTRIBUTING.md)：工作范围、提交格式、签名与 PR。

公共文档不在本仓库复制。许可证为 [AGPL-3.0-only](LICENSE)，统一说明见 [Core 许可证说明](https://github.com/ShitBot-Team/ShitBotUltra/blob/main/docs/licensing.md)。README 后续再写；后续实现范围由维护者指定，测试由维护者自行安排。
