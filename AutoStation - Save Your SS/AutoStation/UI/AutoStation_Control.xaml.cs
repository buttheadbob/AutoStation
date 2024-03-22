using System.Windows;
using System.Windows.Controls;
using AutoStation.Utils;

namespace AutoStation.UI
{
    public partial class AutoStation_Control : UserControl
    {
        public AutoStation_Control()
        {            
            DataContext = AutoStation_Main.Instance!.Config;
            InitializeComponent();
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            AutoStation_Main.Instance?.Save();
            
            if (int.TryParse(Frequency.Text, out int newFrequency))
                Auto.UpdateTimer(newFrequency);
        }
    }
}
