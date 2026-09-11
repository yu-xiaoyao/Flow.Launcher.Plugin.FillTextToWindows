using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.FillTextToWindows.ViewModels;

namespace Flow.Launcher.Plugin.FillTextToWindows.Views
{
    public partial class SettingsControl : UserControl
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsControl(SettingsViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;

            DataContext = viewModel;
        }

        private void OnRecordLeadingKeysClick(object sender, RoutedEventArgs e)
        {
            ShortcutRecorderWindow.Record(Window.GetWindow(this), _viewModel.LeadingKeys, "开始前先发送的按键");
        }

        private void OnRecordNextFieldKeysClick(object sender, RoutedEventArgs e)
        {
            ShortcutRecorderWindow.Record(Window.GetWindow(this), _viewModel.NextFieldKeys, "切换输入框的按键");
        }

        private void OnRecordLastFieldKeysClick(object sender, RoutedEventArgs e)
        {
            ShortcutRecorderWindow.Record(Window.GetWindow(this), _viewModel.LastFieldKeys, "最后一段之后的按键");
        }
    }
}
