using System.Windows.Controls;
using Flow.Launcher.Plugin.FillTextToWindows.ViewModels;

namespace Flow.Launcher.Plugin.FillTextToWindows.Views
{
    public partial class SettingsControl : UserControl
    {
        public SettingsControl(SettingsViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
        }
    }
}
