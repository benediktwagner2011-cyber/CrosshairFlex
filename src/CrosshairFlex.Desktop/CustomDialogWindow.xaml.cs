using System.Windows;

namespace CrosshairFlex.Desktop;

public partial class CustomDialogWindow : Window
{
    private CustomDialogWindow()
    {
        InitializeComponent();
    }

    public static bool ShowInfo(Window owner, string title, string message, string okText = "OK")
    {
        var dialog = new CustomDialogWindow
        {
            Owner = owner,
            Title = title
        };

        dialog.HeadingText.Text = title;
        dialog.MessageText.Text = message;
        dialog.PrimaryButton.Content = okText;
        dialog.SecondaryButton.Visibility = Visibility.Collapsed;
        dialog.ShowDialog();
        return true;
    }

    public static bool ShowConfirm(
        Window owner,
        string title,
        string message,
        string confirmText = "Yes",
        string cancelText = "No")
    {
        var dialog = new CustomDialogWindow
        {
            Owner = owner,
            Title = title
        };

        dialog.HeadingText.Text = title;
        dialog.MessageText.Text = message;
        dialog.PrimaryButton.Content = confirmText;
        dialog.SecondaryButton.Content = cancelText;
        dialog.SecondaryButton.Visibility = Visibility.Visible;

        var result = dialog.ShowDialog();
        return result == true;
    }

    private void PrimaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void SecondaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
