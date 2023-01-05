using System.Windows;
using System.Windows.Controls;
using VRage.Plugins;

namespace AutoStation_SaveYourSS
{
    public partial class AutoStation___Save_Your_SSControl : UserControl
    {

        private AutoStation___Save_Your_SS Plugin { get; }

        public AutoStation___Save_Your_SSControl()
        {            
            DataContext = AutoStation___Save_Your_SS.Instance.Config;
            InitializeComponent();
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Save();
        }
    }
}
