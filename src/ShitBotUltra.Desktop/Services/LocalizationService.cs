using System.ComponentModel;
using System.Globalization;

namespace shitbotteam.ultra.desktop.Services;

/// <summary>Desktop UI language selection. Core and system messages retain their original language.</summary>
public sealed partial class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Current { get; } = new();
    private readonly bool _systemChinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
    private bool _chinese;
    private string _preference = "system";

    private LocalizationService() { _chinese = _systemChinese; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? LanguageChanged;
    public string Preference => _preference;
    public bool IsChinese => _chinese;
    public static string NormalizePreference(string? value) => value is "zh-CN" or "en-US" ? value : "system";
    public string Text(string? english)
    {
        if (string.IsNullOrEmpty(english)) return string.Empty;
        return _chinese && Chinese.TryGetValue(english, out var translated) ? translated : english;
    }
    public void SetPreference(string? preference)
    {
        _preference = NormalizePreference(preference);
        var chinese = _preference == "zh-CN" || (_preference == "system" && _systemChinese);
        if (_chinese == chinese) return;
        _chinese = chinese;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChinese)));
        LanguageChanged?.Invoke();
    }

    private static readonly Dictionary<string, string> Chinese = new(StringComparer.Ordinal)
    {
        ["Overview"] = "概览",
        ["Connections"] = "连接",
        ["Configuration sections"] = "配置分区",
        ["Configuration"] = "配置原文",
        ["Tasks"] = "定时任务",
        ["Whitelist import"] = "白名单导入",
        ["Templates"] = "图片模板",
        ["Plugins"] = "插件",
        ["Events"] = "事件",
        ["Activity logs"] = "活动日志",
        ["Updates"] = "更新",
        ["Settings"] = "设置",
        ["Language"] = "语言",
        ["Follow Windows"] = "跟随系统",
        ["Connection details and local host paths. Admin credentials are kept in memory only."] = "连接信息和本地宿主路径。管理员凭据仅保存在内存中。",
        ["Core management URL"] = "Core 管理地址",
        ["CLI executable"] = "CLI 可执行文件",
        ["Path to sbu.exe"] = "sbu.exe 的路径",
        ["CLI arguments"] = "CLI 启动参数",
        ["Desktop passes this argument string directly to sbu.exe without a shell. Default: serve --no-webui. Override only when using a CLI version with a different documented command."] = "Desktop 会将参数直接传给 sbu.exe，不经过命令行解释器。默认值：serve --no-webui。仅在 CLI 版本的文档要求不同时修改。",
        ["Core configuration directory"] = "Core 配置目录",
        ["The Core owns its database and runtime files. Desktop uses the documented default user configuration directory."] = "数据库和运行文件由 Core 管理。Desktop 使用约定的默认用户配置目录。",
        ["Open configuration directory"] = "打开配置目录",
        ["Admin token"] = "管理员令牌",
        ["Blank: read local token only for loopback"] = "留空时仅为本机连接读取本地令牌",
        ["Start or attach to Core when Desktop opens"] = "打开 Desktop 时启动或连接 Core",
        ["Minimize to the system tray when closing the window"] = "关闭窗口时最小化到系统托盘",
        ["Save preferences"] = "保存设置",
        ["Discard changes and reload"] = "放弃修改并重新读取",
        ["Connect"] = "连接",
        ["Unsaved preferences are open. Save them or explicitly discard them before reloading."] = "设置尚未保存。请先保存或明确放弃修改，再重新读取。",
        ["Credentials are never written to Desktop settings."] = "凭据不会写入 Desktop 设置。",
        ["Desktop preferences saved."] = "Desktop 设置已保存。",
        ["Enter the remote Core admin token explicitly. The local token file is only used for loopback connections."] = "请手动输入远程 Core 的管理员令牌。本地令牌文件仅用于本机连接。",
        ["Enter an admin token or create secrets/admin.token in the Core configuration directory."] = "请输入管理员令牌，或在 Core 配置目录创建 secrets/admin.token。",
        ["Connected. The token is held in memory for this session only."] = "已连接。令牌仅在本次会话中保存在内存里。",
        ["Opened the Core configuration directory."] = "已打开 Core 配置目录。",
        ["The CLI executable path is required."] = "必须填写 CLI 可执行文件路径。",
        ["CLI arguments must be 2048 characters or fewer and cannot contain NUL."] = "CLI 参数最多 2048 个字符，且不能包含 NUL。",
        ["Discard Desktop preferences?"] = "放弃 Desktop 设置？",
        ["The current endpoint or CLI path has not been saved. Reloading replaces these values with the saved preferences."] = "当前地址或 CLI 路径尚未保存。重新读取会用已保存的设置替换当前内容。",
        ["Discard and reload"] = "放弃并重新读取",
        ["Keep changes"] = "保留修改",
    };
}
