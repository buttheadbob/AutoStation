using System.Windows;
using System.Windows.Controls;

namespace AutoStation
{
    public partial class AutoStation_Control : UserControl
    {
        public AutoStation_Control()
        {            
            DataContext = AutoStation_Main.Instance.Config;
            InitializeComponent();
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            AutoStation_Main.Instance.Save();
        }
    }
}
