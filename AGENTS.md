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
  **别用 `System.Windows.Clipboard`**：它是 OLE 剪贴板，要求线程是 STA，而填充跑在 `Task.Run` 的线程池线程（MTA）上，
  会抛 `ThreadStateException: Current thread must be set to single thread apartment (STA) mode before OLE calls can be made.`
  「还原剪贴板」也是同一套：`Capture()` 用 `EnumClipboardFormats` 把每个格式的字节拷一份，
  `Restore()` 再 `EmptyClipboard` + 逐个 `SetClipboardData` 写回去。枚举时跳过 `CF_BITMAP` / `CF_PALETTE` /
  `CF_ENHMETAFILE` 这类存的是 GDI 句柄、以及 `CF_PRIVATEFIRST~LAST`、`CF_GDIOBJFIRST~LAST` 的格式，
  它们 `GetClipboardData` 拿到的不是能拷走字节的 HGLOBAL。
  捕获不到内容（打开剪贴板失败、本来就是空的）就返回 `Captured=false` / 空列表，还原时直接放弃，免得把剪贴板清空。
- **一次填充只跑一遍**：窗口隐藏时 `VisibilityChanged` 可能连着触发好几次（日志里见过同一个任务 144ms 内进来两次），
  每个事件都会 `Task.Run` 一个 `StartFill`。所以 `FillTextTaskMetadata.TryStart()` 用 `Interlocked` 认领，
  同一个任务只有第一个后台线程真正执行，否则会并发粘贴、内容重复。
  换新任务不算重复：新 metadata 是另一个实例，老的会在 `WaitMills` 里发现 `Id` 变了自行退出。
- **按键走 `SendInput`**：`Interop/KeyboardSimulator.cs`。
- **按键可配置**：`Keys/KeyParser.cs` 解析 `Settings.NextFieldKeys` 等配置项，
  语法是「组合之间用逗号或空格分隔，组合内部用 `+`」。设置面板会实时显示解析结果。
- **按键可以录，不用手写**：三个按键字段（设置面板）、每条记录的自定义按键和数据行的每一段（数据管理窗口）
  旁边都有「录制」按钮，
  打开 `Views/ShortcutRecorderWindow.xaml`。上半部分是 `ViewModels/ShortcutRecorderViewModel.cs` 里的记录结果
  （`ChordItem` 一条一个组合，能删、能挪），下半部分是 `Views/KeyboardLayout.cs` 里的键盘图——
  纯数据，位置全在 `AllCaps` 里写死（一个字母键 = 4 格），界面那边按格子铺成一个 `Grid`，测试在 `TestDemo/KeyboardLayoutTest.cs`。
  录一条只走 `KeyChord.Create(...).Text`，所以写进配置的一定是规范写法。几个坑：
  - 物理按键走 `Window.PreviewKeyDown`（tunneling 里最外层的，比子控件先拿到），一律 `e.Handled = true`，
    这样 Space / Enter 不会误按对话框自己的按钮。**按钮里别写 `_` 访问键**：按住 Alt 的访问键不走按键事件，拦不住。
  - `Esc` 固定当取消、不录进去，要录 Esc 就点键盘图上的键帽。
  - 修饰键统一用不分左右的键码（`KeyCodes.Control`，不是 `LeftControl`）：混着写会变成 `Ctrl+LCtrl+V`，存进配置没法看。
    按住哪些修饰键问 `GetKeyState`（`Interop/NativeMethods.cs` 里新加的），不读 `Keyboard.Modifiers`——
    传通用键码时左右哪个按住都算数，也不会漏掉对话框打开前就按着的键。
  - **名字表里没有的键一律不录**（`KeyCodes.IsKnown`）：`GetName` 对它们会给出 `0x0C` 这种解析不回来的写法，
    存下去 `KeyListEditor.Push` 直接把这串按键清空。真实会踩到的是 NumLock 关着时的小键盘 5（VK_CLEAR 0x0C）。
  - `Alt+Tab`、`Win+字母`、`Ctrl+Alt+Del` 这类被系统抢走的键录不到（键盘图也绕不过去），小键盘回车和主回车是同一个键。
  - 对话框的 `Owner` 传按钮所在的那个窗口（`Window.GetWindow(this)`）。别传 Flow Launcher 的主窗口——
    它会隐藏，跟着一起藏起来的模态对话框就成了看不见的窗口。
  - 键盘图的坐标是手写的表，改完跑一下 `TestDemo/KeyboardLayoutTest.cs`（每行都必须正好铺满）。
- **保存的记录**：`Data/FillEntryStore.cs` 用 SQLite 存，两张表——
  主表 `FillEntries` 存名称、三个按键、两个开关和四个可空的延迟/剪贴板字段，从表 `FillEntryLines`
  （`EntryId` / `Value` / `LeadingKeys` / `NextFieldKeys` / `LastFieldKeys` / `SortOrder`）
  存每一段数据外加这一段的按键，`SortOrder` 从 1 开始就是粘贴顺序。更新记录时行数据整体删掉重插，删除记录靠
  `ON DELETE CASCADE`（所以每次开连接都会 `PRAGMA foreign_keys = ON`）。
  数据库放在 `PluginMetadata.PluginSettingsDirectoryPath` 下（跟着 Flow Launcher 的数据目录走，便携模式也对）。
  注意这条路径属性是 Flow Launcher 2.x + `Flow.Launcher.Plugin` 4.7.0 起才有的，插件必须用 4.7.0 以上。
  查记录的 `SelectEntries` 里，从表的三个按键列**必须起别名**（`AS LineLeadingKeys` 这种）：
  和主表同名列重名的话 `GetOrdinal` 拿到的是主表那一列。
- **三层按键配置**：全局配置（设置面板）→ 主表配置（`FillEntry.LeadingKeys` 等，`UseCustomSettings` 总开关）
  → 数据行配置（`FillEntryLine` 上的三个按键，`UseLineSettings` 总开关）。
  第三层是叠在第一层或第二层上用的，不是替换（`FillTextHelper.DoStartFillTextAsync` 的执行顺序）：
  主表开始前按键 → 第 1 段的开始前按键（**只认第 1 段**，整批只在第一个粘贴之前发一次）
  → 循环｛粘贴 + 该段粘贴后按键（非空顶掉主表，空则回落主表）｝
  → 最后一段的最后之后按键 → 主表的最后一段之后按键。
  `FillTextHelper.ToFillTextItem(FillEntry, Settings)` 负责把记录摊成这个结构；模式关着时行上的按键一律置空。
  界面上每行的按键整块跟着「数据行配置模式」显示 / 隐藏，其中「开始前」只画在第 1 段上——
  后面几段填了也不会执行，不如不给这个框。
  `FillTextHelper.DescribeFlow(FillTextItem)` 把这份顺序渲染成一步一行的操作流程文本（预览 / 打日志用），
  **它和 `DoStartFillTextAsync` 是照着写的，改执行顺序时两边一起改**。
- **延迟/剪贴板是逐项的，按键是整层的**：`BeforeFillDelayMs` / `PasteDelayMs` / `KeyDelayMs` /
  `RestoreClipboard` 在库里可空，
  NULL 表示「跟着全局设置走」（`FillEntry.ResolveSettings` 逐项回落，`ToDbValue` 负责把 null 写成 SQL NULL）。
  所以一条记录只勾「自定义按键」、只改「按键间隔」就行，另外几项继续跟着设置面板变。
  界面上这几项**不跟着「自定义按键」开关变灰**，数字框留空 / 写错都按 null 算，旁边灰字说明会跟到哪个全局值；
  剪贴板那个勾选框是三态的（`IsThreeState`），半选 = null。
  三个延迟框共用 `ViewModels/DelayEditor.cs`（和 `KeyListEditor` 一个路子：存原文、用的时候才解析）。
  **别把数字框直接绑到 `int?` 上**：清空时 WPF 不会写出 null，而是**保留旧值**，
  看着像清掉了实际还生效（单独建了个 WPF 小程序实测过）。
- **开始前等待**：`BeforeFillDelayMs` 是 `DoStartFillTextAsync` 里最先等的那一段（发任何按键之前），
  走 `BuildFillTextItem` 进 `FillTextItem`，和另外几个延迟一样，记录里留空就跟全局走。
  曾在末尾加过一个对称的 `LastFillDelayMs`，实测没用就删了，别再往回加。
  注意设置面板上的数字框还是直接绑 `Settings.XxxDelayMs`（全局值不能为空，没有「留空」这一说），
  所以清空那个框同样是「保留旧值」，没跟记录那边一样做 `DelayEditor`。
- **表结构变了就重建，不做迁移**：`EnsureCreated` 拿 `RequiredColumns` 里那份列清单查 `pragma_table_info`，
  表已经在了但缺列就 `DROP TABLE` 重建（先删从表，主表被外键引用着，顺序反了删不掉）。
  也就是说改表结构不用写迁移代码，老库直接丢掉、记录重新录一遍就行；表还没建出来的新库不算，`DbDDL` 直接建。
- **管理页面是独立窗口**：`Views/ManagementWindow.xaml`。**故意不设 Owner** —— Flow Launcher 的主窗口
  在 Action 返回 true 之后会被隐藏，设了 Owner 的话这个窗口会跟着一起消失。

目录结构：

    Main.cs                     插件入口、Query、后台填充流程
    Arguments.cs                把 ftw 后面的文字切成多段
    Settings.cs                 配置模型（INotifyPropertyChanged，供设置面板实时预览）
    Data/                       FillEntry 实体 + SQLite 存储
    Keys/                       按键名 ↔ 虚拟键码、按键序列解析
    Interop/                    Win32 剪贴板与 SendInput 封装
    ViewModels/ Views/          设置面板 + 数据管理窗口 + 按键录制对话框（KeyboardLayout / ShortcutRecorderWindow）
    Images/FillTextToWindows.png  插件图标（tools/make-icon.ps1 生成）

    TestDemo/                   测试代码放这里（不参与打包）。默认不引用插件工程，也不编译测试文件；
                                要跑测试加 `-p:WithClipboardTest=true`，
                                这时才把插件工程引进来（插件里用 `<InternalsVisibleTo Include="TestDemo" />`
                                把 internal 的 Interop / Keys 开放给它）：
                                dotnet run --project TestDemo -p:WithClipboardTest=true -- --clipboard-test
                                dotnet run --project TestDemo -p:WithClipboardTest=true -- --keyboard-test
                                键盘图排版看不准、或者改了对话框的 XAML，可以用预览模式真建一次窗口、
                                自己发按键录一遍再截图（要抢几秒焦点，别在正打字的时候跑）：
                                dotnet run --project TestDemo -p:WithClipboardTest=true -- --recorder-preview [png]


## 管理页面

添加一个管理页面, 用于提交将数据保存, 保存使用Sqlite(Microsoft.Data.Sqlite).
数据保存如下
1. 名称(用于 Flow Launcher 搜索)
2. 数据(多个复制粘贴组)
3. 自定义按键. 如果没有则使用当前插件全局的按键
   (1). 开始前先发送的按键
   (2). 切换输入框的按键
   (3). 最后一段之后的按键
4. 延迟和剪贴板(每一项都可以留空, 留空就跟着全局设置走)
   (1). 开始前等待(毫秒)
   (2). 粘贴后等待(毫秒)
   (3). 按键间隔(毫秒)
   (4). 填充完成后把剪贴板还原成原来的文本
5. 数据行配置(可选, 勾「数据行配置模式」才生效): 每一段自己的
   开始前按键 / 粘贴后按键 / 最后一段之后按键, 和上面的自定义按键叠加
