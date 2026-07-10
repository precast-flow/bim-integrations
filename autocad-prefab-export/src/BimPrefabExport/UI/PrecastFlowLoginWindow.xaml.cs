using System.Windows;
using System.Windows.Media;
using BimPrefabExport.Services;

namespace BimPrefabExport.UI;

public partial class PrecastFlowLoginWindow : Window
{
    public PrecastFlowLoginWindow()
    {
        InitializeComponent();
        PrecastFlowSessionManager.Reload();
        ApiUrlBox.Text = PrecastFlowSessionManager.Session.ApiBaseUrl;
        EmailBox.Text = PrecastFlowSessionManager.Session.Email ?? "admin@precastflow.local";
        PasswordBox.Password = "ChangeMe123!";
    }

    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        await RunSafeAsync(async () =>
        {
            SetBusy(true, "Bağlantı test ediliyor…");
            using var client = new PrecastApiClient(ApiUrlBox.Text.Trim());
            await client.TestConnectionAsync().ConfigureAwait(true);
            SetStatus("Sunucuya erişim başarılı.", false);
        });
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        await RunSafeAsync(async () =>
        {
            var apiUrl = ApiUrlBox.Text.Trim();
            var email = EmailBox.Text.Trim();
            var password = PasswordBox.Password;
            if (string.IsNullOrWhiteSpace(apiUrl) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                SetStatus("API adresi, e-posta ve şifre gerekli.", true);
                return;
            }

            SetBusy(true, "Giriş yapılıyor…");
            await PrecastFlowSessionManager.LoginAsync(apiUrl, email, password).ConfigureAwait(true);
            PasswordBox.Clear();
            DialogResult = true;
            Close();
        });
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async Task RunSafeAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy, string? status = null)
    {
        TestButton.IsEnabled = !busy;
        LoginButton.IsEnabled = !busy;
        ApiUrlBox.IsEnabled = !busy;
        EmailBox.IsEnabled = !busy;
        PasswordBox.IsEnabled = !busy;
        if (!string.IsNullOrWhiteSpace(status))
            SetStatus(status, false);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush(
            isError ? System.Windows.Media.Color.FromRgb(0xB0, 0x00, 0x20) : System.Windows.Media.Color.FromRgb(0x32, 0x32, 0x32));
    }
}
