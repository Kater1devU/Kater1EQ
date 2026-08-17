using System.Windows;

namespace Kater1EQ
{
    public partial class PromptDialog : Window
    {
        public string ResultText { get; private set; } = string.Empty;

        public PromptDialog(string prompt, string defaultValue = "")
        {
            InitializeComponent();
            PromptText.Text = prompt;
            InputTextBox.Text = defaultValue;
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultText = InputTextBox.Text.Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
