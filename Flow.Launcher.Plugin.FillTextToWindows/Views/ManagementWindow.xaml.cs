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
