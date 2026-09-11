Flow.Launcher.Plugin.FillTextToWindows
==================

A plugin for the [Flow launcher](https://github.com/Flow-Launcher/Flow.Launcher).

把多个文本**依次粘贴到当前窗口的多个输入框**里：Flow Launcher 隐藏、焦点回到刚才的程序之后，
插件会依次模拟 `Ctrl+V`，并在每段之间按下你配置的按键（默认 `Tab`）跳到下一个输入框。

适合填注册表单、登录框、地址栏这一类「一个字段一个输入框」的场景。

### Usage

    ftw <arguments>

| 写法 | 说明 |
| --- | --- |
| `ftw alice 123456 a@b.com` | 按空格切分成三段，依次粘贴 |
| `ftw "hello world" 123456` | 双引号里的内容视为一整段，可以带空格 |
| `ftw hello world\|123456` | 只要出现 `\|`，就改成按 `\|` 切分，值里可以随便带空格 |
| `ftw 登录表单` | 搜到数据管理里保存过的记录，回车直接按那条记录填充 |

输入 `ftw` 后直接回车会显示用法、数据管理入口和设置入口。

名称完全对上的时候不会再显示「把这段文字当一段填充」的结果，免得点错；真想按字面粘贴就加个引号，
比如记录名是 `a b` 时，`ftw "a b"` 会按字面粘贴 `a b`。

### 数据管理

`ftw` 里点「打开数据管理」会弹出一个独立窗口，把常用的那几组数据存下来（SQLite，存在 Flow Launcher
给这个插件分配的设置目录里）：

- **名称**：Flow Launcher 就是靠它搜索的
- **数据**：一个输入框一段，点「+ 添加数据」加一个，点每行后面的「删除」去掉一个；
  序号就是粘贴顺序，空白的输入框会被跳过
- **自定义配置**：不勾选时这条记录完全走插件的全局配置；勾上后那 6 个字段才生效，
  勾上的一瞬间会先用当前全局值填好，所以只想改「切换键」的话改那一个就行

窗口左边列出所有记录，右边是编辑表单；「新建」清空表单，「删除」删掉选中的记录，
表单有没保存的改动时切换记录或关窗口都会先问一句。

数据库位置：`%APPDATA%\FlowLauncher\Settings\Plugins\Flow.Launcher.Plugin.FillTextToWindows\FillTextToWindows.db`
（Flow Launcher 开了便携模式的话在它的 `UserData` 目录下）。

### 工作流程

1. 按回车，Flow Launcher 隐藏窗口，焦点回到你刚才在用的程序，插件这时候开始干活；
2. 每段文本：写入剪贴板 → `Ctrl+V` → 按「切换输入框的按键」→ 下一段；
3. 最后一段粘贴完，按「最后一段之后的按键」（默认不按）。

任何一步失败（剪贴板被占用、`SendInput` 被拒绝、按键写法解析不了）都会停下来并写进 Flow Launcher 日志，
不会往错误的窗口里乱敲。

### 设置

在 Flow Launcher 设置 → 插件 → FillTextToWindows 里配置：

| 配置 | 默认值 | 说明 |
| --- | --- | --- |
| 开始前先发送的按键 | 空 | 进去的时候焦点还不在第一个输入框时用，例如 `Ctrl+Home` |
| 切换输入框的按键 | `Tab` | 每段粘贴完之后按，跳下一个输入框；也可以写 `Down`、`Shift+Tab` |
| 最后一段之后的按键 | 空 | 想填完直接提交就写 `Enter` |
| 粘贴后等待（毫秒） | 40 | 目标程序处理 `Ctrl+V` 的时间，卡顿时调大 |
| 按键间隔（毫秒） | 40 | 连续按键之间的间隔，漏键时调大 |
| 填充完成后还原剪贴板 | 关 | 打开后把剪贴板还原成原来的文本（图片／文件等非文本内容无法还原） |

### 按键写法

一个组合里用 `+` 连接修饰键和主键，要连按几个键就用逗号隔开，`+` 两边、逗号前后都可以带空格，不区分大小写。

| 写法 | 意思 |
| --- | --- |
| `Tab` ／ `Down` ／ `Enter` | 一个键 |
| `Ctrl+A` ／ `Ctrl + A` | 组合键 |
| `Down, Down, Enter` | 连按三个键，也可以用空格分隔：`Down Down Enter` |
| `Ctrl + \+` | 按住 Ctrl 再按加号键本身 |
| `Ctrl+Home` | 回到开头 |

整个键盘都能写：

- 字母数字、`F1`～`F24`、小键盘 `Num0`～`Num9` ／ `NumAdd` ／ `NumMultiply` ／ `NumDecimal` ／ `NumDivide`
- `Tab` `Enter` `Esc` `Space` `Backspace` `Delete` `Insert` `Home` `End` `PageUp` `PageDown`
  `Left` `Right` `Up` `Down` `PrtSc` `CapsLock` `NumLock` `ScrollLock` `Pause` `Apps` `Sleep`
- 标点就写那个字符本身：`,` `-` `=` `[` `]` `;` `'` `/` `\` `` ` `` ，
  以及它们的上档字符 `+` `_` `{` `}` `:` `"` `?` `|` `!` `@` `#` `$` `%` `^` `&` `*` `(` `)` `~` `<` `>`
- 修饰键 `Ctrl` `Alt` `Shift` `Win`，要区分左右就写 `LCtrl` `RCtrl` `LShift` `RShift` `LAlt` `RAlt` `RWin`
- 多媒体／浏览器键 `VolumeUp` `VolumeMute` `MediaNext` `MediaPlay` `BrowserBack` …

上档字符会自动带上 Shift，所以写 `+` 就等于按 `Shift` + `=` 那个物理键。

`+`、`,`、`\` 自己也是按键，但它们同时是语法字符，写的时候前面加一个 `\` 转义：
`Ctrl + \+` 是「Ctrl + 加号键」，`Ctrl+\,` 是「Ctrl + 逗号键」，`\\` 是反斜杠键。

按键在配置里存成字符串数组，一个元素就是一个组合：

    ["Ctrl+Home"]
    ["Ctrl+\\+", "Tab"]         // 先 Ctrl+加号，再 Tab
    ["Down", "Down", "Enter"]

JSON 里 `\` 自己还要再转义一层，所以文件里看到的是 `Ctrl+\\+`，
界面上（设置面板和数据管理窗口）显示的都是 `Ctrl+\+`。


### 开发

    pwsh -File debug.ps1     # 编译并安装到本机 Flow Launcher 的插件目录，然后重启 Flow Launcher

    pwsh -File release.ps1   # 打 Release 包
