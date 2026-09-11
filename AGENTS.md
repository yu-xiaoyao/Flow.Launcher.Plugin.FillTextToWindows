# 自动填充数据

- 开始说明: `Dotnet Flow Launcher Plugin`



### 功能说明

Flow Launcher 的结果对应了一个 List<String> 结果, 我想将这个结果粘贴到多个输入框

### 实现说明

1. 在 Flow Launcher 窗口隐藏后, 也就是窗口焦点回到了最后的软件后.

2. 开始使用模拟发送 `Ctrl + C` 和  `Ctrl + V` 的方式复制和粘贴

3. 由于是多个, 每组复制粘贴后使用 `Tab` 切换到下一字符



### 将插件安装到 Flow Launcher 调试

直接使用 `pwsh.exe` 也就是 PowerShell 7 执行  `debug.ps1`脚本



### 实现要点

- **必须先返回、再按键**：Flow Launcher 是「先执行 `Result.Action`，再 `Hide()`」（见 `MainViewModel.ExecuteResultAsync`），
  而且 `Hide()` 本身是 `async void`，里面还有 `Task.Delay(50)`。所以 `Action` 里只做两件事——
  记住 Flow Launcher 主窗口句柄、把工作丢给 `Task.Run`，然后立刻 `return true`。
- **等焦点真的切走**：后台任务轮询 `GetForegroundWindow()`，直到它不再是 Flow Launcher 的主窗口才继续，
  比固定 sleep 稳；超时（默认 2000ms）就放弃，避免把内容打进 Flow Launcher 自己的搜索框。
- **剪贴板走裸 Win32**：`Interop/ClipboardHelper.cs` 直接调 `OpenClipboard`/`SetClipboardData`，
  不依赖 WPF / WinForms，任何线程都能用。
- **按键走 `SendInput`**：`Interop/KeyboardSimulator.cs`。
- **按键可配置**：`Keys/KeyParser.cs` 解析 `Settings.NextFieldKeys` 等配置项，
  语法是「组合之间用逗号或空格分隔，组合内部用 `+`」。设置面板会实时显示解析结果。
- **保存的记录**：`Data/FillEntryStore.cs` 用 SQLite 存，两张表——
  主表 `FillEntries` 存名称和那 8 个配置字段，从表 `FillEntryLines`（`EntryId` / `Value` / `SortOrder`）
  存每一段数据，`SortOrder` 从 1 开始就是粘贴顺序。更新记录时行数据整体删掉重插，删除记录靠
  `ON DELETE CASCADE`（所以每次开连接都会 `PRAGMA foreign_keys = ON`）。
  数据库放在 `PluginMetadata.PluginSettingsDirectoryPath` 下（跟着 Flow Launcher 的数据目录走，便携模式也对）。
  注意这条路径属性是 Flow Launcher 2.x + `Flow.Launcher.Plugin` 4.7.0 起才有的，插件必须用 4.7.0 以上。
- **表结构变了就重建**：`EnsureCreated` 发现老结构的 `FillEntries.ValuesJson` 列就直接
  `DROP TABLE` 重建（先删从表，主表被外键引用着，顺序反了删不掉），不做数据迁移。
- **管理页面是独立窗口**：`Views/ManagementWindow.xaml`。**故意不设 Owner** —— Flow Launcher 的主窗口
  在 Action 返回 true 之后会被隐藏，设了 Owner 的话这个窗口会跟着一起消失。

目录结构：

    Main.cs                     插件入口、Query、后台填充流程
    Arguments.cs                把 ftw 后面的文字切成多段
    Settings.cs                 配置模型（INotifyPropertyChanged，供设置面板实时预览）
    Data/                       FillEntry 实体 + SQLite 存储
    Keys/                       按键名 ↔ 虚拟键码、按键序列解析
    Interop/                    Win32 剪贴板与 SendInput 封装
    ViewModels/ Views/          设置面板 + 数据管理窗口
    Images/FillTextToWindows.png  插件图标（tools/make-icon.ps1 生成）


## 管理页面

添加一个管理页面, 用于提交将数据保存, 保存使用Sqlite(Microsoft.Data.Sqlite).
数据保存如下
1. 名称(用于 Flow Launcher 搜索)
2. 数据(多个复制粘贴组)
3. 自定义配置. 如果没有则使用当前插件全局的配置
   (1). 开始前先发送的按键
   (2). 切换输入框的按键
   (3). 最后一段之后的按键
   (4). 粘贴后等待(毫秒)
   (5). 按键间隔(毫秒)
   (6). 焦点等待上限(毫秒)
   (7).填充完成后把剪贴板还原成原来的文本
