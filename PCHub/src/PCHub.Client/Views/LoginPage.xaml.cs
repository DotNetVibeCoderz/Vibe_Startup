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
    }

    private async void LoginBtn_Click(object sender, RoutedEventArgs e)
    {
        LoginBtn.IsEnabled = false;
        LoginBtn.Content = "Logging in...";
        ErrorText.Visibility = Visibility.Collapsed;

        try
        {
            var result = await _api.LoginAsync(UsernameBox.Text, PasswordBox.Password);
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
