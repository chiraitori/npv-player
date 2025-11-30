using System.Windows;

namespace NpvPlayer;

public partial class InputDialog : Window
{
    public string InputText => txtInput.Text;

    public InputDialog(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        lblPrompt.Text = prompt;
        txtInput.Focus();
    }

    private void BtnOK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
