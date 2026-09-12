using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.FillTextToWindows.Data;
using Flow.Launcher.Plugin.FillTextToWindows.Fill;
using Flow.Launcher.Plugin.FillTextToWindows.Keys;
using Flow.Launcher.Plugin.FillTextToWindows.Util;
using Flow.Launcher.Plugin.FillTextToWindows.ViewModels;
using Flow.Launcher.Plugin.FillTextToWindows.Views;

namespace Flow.Launcher.Plugin.FillTextToWindows
{
    /// <summary>
    /// 把多段文本依次粘贴到当前窗口的多个输入框里。
    /// <para>
    /// 两种用法：直接用 <c>ftw 文本1 文本2</c>，或者把常用的一组数据存进 SQLite，
    /// 之后用 <c>ftw 名称</c> 搜出来。
    /// </para>
    /// <para>
    /// Flow Launcher 是「先执行结果、再隐藏窗口」的，所以这里在 Action 里立刻返回并丢一个后台任务，
    /// 等窗口隐藏、焦点回到目标程序之后再开始模拟 Ctrl+V / Tab。
    /// </para>
    /// </summary>
    public class FillTextToWindows : IPlugin, ISettingProvider
    {
        public const string IcoPath = "Images\\FillTextToWindows.png";

        private PluginInitContext _context;
        private Settings _settings;

        private SettingsViewModel _viewModel;

        private FillEntryStore _store;

        /// <summary>数据库没起来的时候（比如文件被占用）就退化成只能用临时输入。</summary>
        private bool _storeReady;

        private ManagementWindow _managementWindow;

        public void Init(PluginInitContext context)
        {
            _context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>() ?? new Settings();
            _viewModel = new SettingsViewModel(_settings);

            // add VisibilityChanged Event
            _context.API.VisibilityChanged += VisibilityChangedEventHandler;

            InnerLogger.SetAsFlowLauncherLogger(context, LoggerLevel.DEBUG);

            try
            {
                _store = new FillEntryStore(ResolveDatabasePath(context));
                _store.EnsureCreated();
                _storeReady = true;
            }
            catch (Exception ex)
            {
                _storeReady = false;
                InnerLogger.Logger.Error("初始化保存记录的数据库失败, 本次只能用临时输入.", ex);
            }
        }

        public Control CreateSettingPanel()
        {
            return new SettingsControl(_viewModel);
        }

        private void VisibilityChangedEventHandler(object sender, VisibilityChangedEventArgs args)
        {
            if (!args.IsVisible)
            {
                StartFill();
            }
        }

        private void StartFill()
        {
            // 用 _ = 丢掉 Task 的话异常只会变成「Unobserved task exception」，
            // 这里自己接住，日志里能直接看到是填充的哪一步炸的。
            _ = Task.Run(async () =>
            {
                try
                {
                    await FillTextHelper.StartFill();
                }
                catch (Exception ex)
                {
                    InnerLogger.Logger.Error("填充过程中出错. ", ex);
                }
            });
        }

        private static void _setStartFillItem(IReadOnlyList<string> values, Settings settings)
        {
            FillTextHelper.ResetFill();
            FillTextHelper.SetFillItem(FillTextHelper.ToFillTextItem(values, settings));
        }

        private static void _setStartFillItem(FillEntry entry, Settings settings)
        {
            FillTextHelper.ResetFill();
            FillTextHelper.SetFillItem(FillTextHelper.ToFillTextItem(entry, settings));
        }


        public List<Result> Query(Query query)
        {
            FillTextHelper.ResetFill();

            var search = (query.Search ?? string.Empty).Trim();

            if (search.Length == 0)
            {
                return BuildHomeResults();
            }

            var results = new List<Result>();
            var entries = SearchEntries(search);

            foreach (var entry in entries)
            {
                results.Add(BuildEntryResult(entry, search));
            }

            // 名称完全对上的时候就别再显示「把这段文字当一段填充」了，容易点错
            var hasExactNameMatch =
                entries.Any(entry => string.Equals(entry.Name, search, StringComparison.OrdinalIgnoreCase));

            if (!hasExactNameMatch)
            {
                var values = Arguments.Parse(search);
                if (values.Count > 0)
                {
                    results.Add(BuildFillResult(values));
                }
            }

            if (results.Count == 0)
            {
                results.Add(_storeReady
                    ? BuildManageResult(
                        "没有匹配的记录",
                        "回车打开数据管理，把常用的这组数据存下来")
                    : BuildManageResult(
                        "数据库没初始化成功，这次只能用临时输入",
                        "详细原因看 Flow Launcher 日志"));
            }

            return results;
        }

        private List<Result> BuildHomeResults()
        {
            var results = new List<Result>
            {
                new()
                {
                    Title = "ftw 文本1 文本2 文本3 ...",
                    SubTitle = "按回车后切回刚才的窗口，依次 Ctrl+V 粘贴，并用 Tab 跳到下一个输入框。",
                    IcoPath = IcoPath,
                    Score = 1000,
                    Action = _ => false,
                },
                new()
                {
                    Title = "ftw 名称",
                    SubTitle = "搜到数据管理里保存过的记录，回车直接按那条记录填充。",
                    IcoPath = IcoPath,
                    Score = 950,
                    Action = _ => false,
                },
                new()
                {
                    Title = "带空格的内容用双引号包起来，或者用 | 分隔",
                    SubTitle = "ftw \"hello world\" 123456   等于   ftw hello world|123456",
                    IcoPath = IcoPath,
                    Score = 900,
                    Action = _ => false,
                },
            };

            if (_storeReady)
            {
                var count = SearchEntries(string.Empty).Count;
                results.Add(BuildManageResult(
                    count == 0 ? "打开数据管理（还没有保存过记录）" : $"打开数据管理（已保存 {count} 条记录）",
                    "新增、修改、删除保存好的填充记录"));
            }

            results.Add(new Result
            {
                Title = "打开 FillTextToWindows 设置",
                SubTitle = "自定义「切换输入框的按键」「最后一段之后的按键」以及各项延迟。",
                IcoPath = IcoPath,
                Score = 800,
                Action = _ =>
                {
                    _context.API.OpenSettingDialog();
                    return false;
                },
            });

            return results;
        }

        private Result BuildFillResult(IReadOnlyList<string> values)
        {
            return new Result
            {
                Title = $"依次填充 {values.Count} 个输入框",
                SubTitle = Preview(values) + "   ·   " + DescribeFlow(_settings),
                IcoPath = IcoPath,
                Score = 1000,
                Action = _ =>
                {
                    // StartFill(values, _settings.Clone());
                    _setStartFillItem(values, _settings.Clone());
                    return true;
                },
            };
        }

        private Result BuildEntryResult(FillEntry entry, string search)
        {
            var subtitle = Preview(entry.Values.Select(line => line.Value))
                           + "   ·   "
                           + DescribeFlow(entry.ResolveSettings(_settings))
                           + (entry.UseCustomSettings ? "   ·   自定义配置" : string.Empty)
                           + (entry.UseLineSettings ? "   ·   每段独立按键" : string.Empty);

            return new Result
            {
                Title = entry.Name,
                SubTitle = subtitle,
                IcoPath = IcoPath,
                Score = MatchScore(entry.Name, search),
                Action = _ =>
                {
                    // StartFill(entry.Values, entry.ResolveSettings(_settings));
                    _setStartFillItem(entry, entry.ResolveSettings(_settings));
                    return true;
                },
            };
        }

        private Result BuildManageResult(string title, string subtitle)
        {
            return new Result
            {
                Title = title,
                SubTitle = subtitle,
                IcoPath = IcoPath,
                Score = 100,
                Action = _ =>
                {
                    OpenManagementWindow();
                    return true;
                },
            };
        }

        /// <summary>
        /// 打开数据管理窗口。已经开着就把它激活，不重复开。
        /// </summary>
        private void OpenManagementWindow()
        {
            if (!_storeReady)
            {
                InnerLogger.Logger.Warn("数据库没初始化成功，数据管理窗口打不开。");
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            dispatcher.Invoke(() =>
            {
                if (_managementWindow is { IsLoaded: true })
                {
                    if (_managementWindow.WindowState == WindowState.Minimized)
                    {
                        _managementWindow.WindowState = WindowState.Normal;
                    }

                    _managementWindow.Activate();
                    return;
                }

                var window = new ManagementWindow(new ManagementViewModel(_store, _settings));
                window.Closed += (_, _) => _managementWindow = null;

                _managementWindow = window;
                window.Show();
            });
        }

        private List<FillEntry> SearchEntries(string search)
        {
            if (!_storeReady)
            {
                return new List<FillEntry>();
            }

            try
            {
                return _store.Search(search);
            }
            catch (Exception ex)
            {
                InnerLogger.Logger.Error("读取保存的记录失败。", ex);
                return new List<FillEntry>();
            }
        }

        /// <summary>
        /// 数据库放在 Flow Launcher 给这个插件分配的设置目录里，跟着 Flow Launcher 的数据目录走（含便携模式）。
        /// </summary>
        private static string ResolveDatabasePath(PluginInitContext context)
        {
            var metadata = context.CurrentPluginMetadata;

            var directory = metadata.PluginSettingsDirectoryPath;
            if (string.IsNullOrEmpty(directory))
            {
                // 老版本 Flow Launcher 不给这个路径，按默认位置拼一个
                directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FlowLauncher",
                    "Settings",
                    "Plugins",
                    metadata.AssemblyName ?? metadata.Name ?? "FillTextToWindows");
            }

            return Path.Combine(directory, "FillTextToWindows.db");
        }

        /// <summary>
        /// 名称完全一致排最前，其次是以关键字开头，再次是包含。
        /// </summary>
        private static int MatchScore(string name, string search)
        {
            if (string.Equals(name, search, StringComparison.OrdinalIgnoreCase))
            {
                return 3000;
            }

            if (name.StartsWith(search, StringComparison.OrdinalIgnoreCase))
            {
                return 2000;
            }

            return 1500;
        }

        private static string DescribeFlow(Settings settings)
        {
            var flow = settings.NextFieldKeys is { Count: > 0 }
                ? "按 " + KeyParser.Describe(settings.NextFieldKeys) + " 切换输入框"
                : "不切换输入框";

            if (settings.LastFieldKeys is { Count: > 0 })
            {
                flow += "，最后按 " + KeyParser.Describe(settings.LastFieldKeys);
            }

            return flow;
        }

        private static string Preview(IEnumerable<string> values)
        {
            const int maxItems = 6;

            var shown = values.Take(maxItems).Select(Truncate);
            var text = string.Join("  →  ", shown);

            return values.Count() > maxItems ? text + "  →  ..." : text;
        }

        private static string Truncate(string value)
        {
            const int maxLength = 24;

            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "…";
        }
    }
}