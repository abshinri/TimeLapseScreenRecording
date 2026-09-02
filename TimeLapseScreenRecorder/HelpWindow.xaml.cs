using System.Windows;

namespace TimeLapseScreenRecorder
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
        }

        private void HelpCloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}