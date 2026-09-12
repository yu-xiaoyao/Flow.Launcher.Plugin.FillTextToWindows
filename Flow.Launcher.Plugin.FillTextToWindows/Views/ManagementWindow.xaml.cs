using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Flow.Launcher.Plugin.FillTextToWindows.ViewModels;

namespace Flow.Launcher.Plugin.FillTextToWindows.Views
{
    /// <summary>
    /// 数据管理窗口：左边列出已保存的记录，右边编辑一条。
    /// </summary>
    public partial class ManagementWindow : Window
    {
        private readonly ManagementViewModel _viewModel;

        public ManagementWindow(ManagementViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = viewModel;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_viewModel.ConfirmClose())
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }

        private void OnNewClick(object sender, RoutedEventArgs e) => _viewModel.StartNew();

        private void OnClearClick(object sender, RoutedEventArgs e) => _viewModel.StartNew();

        private void OnDeleteClick(object sender, RoutedEventArgs e) => _viewModel.DeleteSelected();

        private void OnSaveClick(object sender, RoutedEventArgs e) => _viewModel.Save();

        private void OnAddValueClick(object sender, RoutedEventArgs e)
        {
            FocusValueRow(_viewModel.AddValueRow());
        }

        private void OnRemoveValueClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ValueItem item })
            {
                _viewModel.RemoveValueRow(item);
            }
        }

        // 这三个按钮在「自定义配置」那个总开关的 IsEnabled 里，不勾选时点不到：
        // 录进去的东西写在 CustomSettings 上，全局配置模式下会被忽略，也会被当成「没改过」丢掉。

        private void OnRecordLeadingKeysClick(object sender, RoutedEventArgs e)
        {
            ShortcutRecorderWindow.Record(Window.GetWindow(this), _viewModel.Draft.LeadingKeys, "开始前按键");
        }

        private void OnRecordNextFieldKeysClick(object sender, RoutedEventArgs e)
        {
            ShortcutRecorderWindow.Record(Window.GetWindow(this), _viewModel.Draft.NextFieldKeys, "切换输入框");
        }

        private void OnRecordLastFieldKeysClick(object sender, RoutedEventArgs e)
        {
            ShortcutRecorderWindow.Record(Window.GetWindow(this), _viewModel.Draft.LastFieldKeys, "最后一段之后");
        }

        // 数据行上的三个「录制」按钮。整块只有在「数据行配置模式」开着时才显示，
        // 所以不用像上面主表那三个一样担心打开时点不到。

        private void OnRecordValueLeadingKeysClick(object sender, RoutedEventArgs e)
        {
            RecordLineKeys(sender, "开始前按键", static item => item.LeadingKeys);
        }

        private void OnRecordValueNextFieldKeysClick(object sender, RoutedEventArgs e)
        {
            RecordLineKeys(sender, "粘贴后按键", static item => item.NextFieldKeys);
        }

        private void OnRecordValueLastFieldKeysClick(object sender, RoutedEventArgs e)
        {
            RecordLineKeys(sender, "最后一段之后按键", static item => item.LastFieldKeys);
        }

        /// <summary>
        /// 数据行上的「录制」：从那一行的 DataContext 里挑出要写的编辑框，录完写回去。
        /// </summary>
        private void RecordLineKeys(object sender, string label, Func<ValueItem, KeyListEditor> pick)
        {
            if (sender is FrameworkElement { DataContext: ValueItem item })
            {
                ShortcutRecorderWindow.Record(Window.GetWindow(this), pick(item), $"第 {item.Order} 段 {label}");
            }
        }

        /// <summary>
        /// 新加的输入框自动获得焦点，这样「点一下 → 敲 → 再点一下」就能连续录入。
        /// </summary>
        private void FocusValueRow(ValueItem item)
        {
            // 容器要等这一轮布局跑完才生成得出来
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (ValueList.ItemContainerGenerator.ContainerFromItem(item) is not DependencyObject container)
                {
                    return;
                }

                FindTextBox(container)?.Focus();
            }));
        }

        private static TextBox FindTextBox(DependencyObject root)
        {
            if (root is TextBox textBox)
            {
                return textBox;
            }

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var found = FindTextBox(VisualTreeHelper.GetChild(root, i));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
