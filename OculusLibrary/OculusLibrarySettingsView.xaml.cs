using Microsoft.Win32;
using Playnite.Common;
using System.Windows;
using System.Windows.Controls;

namespace OculusLibrary
{
    /// <summary>
    /// Interaction logic for OculusLibrarySettingsView.xaml
    /// </summary>
    public partial class OculusLibrarySettingsView : UserControl
    {
        public OculusLibrarySettingsView()
        {
            InitializeComponent();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog picker = new OpenFileDialog()
            {
                Filter = "ReviveInjector.exe|ReviveInjector.exe|.exe files|*.exe",
                Multiselect = false,
                Title = "Select ReviveInjector.exe",
            };

            var installLocation = Programs.GetReviveUninstallProgram()?.InstallLocation;
            if (installLocation != null)
                picker.InitialDirectory = installLocation;

            var dialogResult = picker.ShowDialog();
            if (dialogResult == true)
            {
                RevivePath.Text = picker.FileName;
            }
        }
    }
}
