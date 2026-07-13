using System.Windows;
using System.Windows.Controls;
using PCHub.Client.Services;

namespace PCHub.Client.Views;

public partial class LoginPage : UserControl
{
    public event EventHandler? LoginSucceeded;
    private readonly ApiService _api;

    public LoginPage()
    {
        InitializeComponent();
        _api = new ApiService(App.ApiBaseUrl);
        UsernameBox.Text = "admin";
        PasswordBox.Password = "Admin123!";
        PasswordTextBox.Text = "Admin123!";
    }

    private void ShowPasswordCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (ShowPasswordCheck.IsChecked == true)
        {
            // Show plain text
            PasswordTextBox.Text = PasswordBox.Password;
            PasswordTextBox.Visibility = Visibility.Visible;
            PasswordBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Show masked
            PasswordBox.Password = PasswordTextBox.Text;
            PasswordTextBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
        }
    }

    private async void LoginBtn_Click(object sender, RoutedEventArgs e)
    {
        LoginBtn.IsEnabled = false;
        LoginBtn.Content = "Logging in...";
        ErrorText.Visibility = Visibility.Collapsed;

        try
        {
            // Ambil password dari mana yang visible
            var password = PasswordBox.Visibility == Visibility.Visible
                ? PasswordBox.Password
                : PasswordTextBox.Text;

            var result = await _api.LoginAsync(UsernameBox.Text, password);
            if (result != null)
            {
                App.AuthToken = result.Token;
                App.UserId = result.UserId;
                App.Username = result.Username;
                _api.SetToken(result.Token);
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorText.Text = "Invalid username or password.";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Connection error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoginBtn.IsEnabled = true;
            LoginBtn.Content = "Login";
        }
    }
}
