using System.Windows;
using BimPrefabExport.UI;

namespace BimPrefabExport.Services;

/// <summary>Ortak PrecastFlow oturum yönetimi — palet ve login penceresi paylaşır.</summary>
public static class PrecastFlowSessionManager
{
    private static readonly BimPrefabSessionStore Store = BimPrefabSessionStore.Default;
    private static PrecastApiClient? _client;

    public static BimPrefabSessionData Session { get; private set; } = new();
    public static PrecastApiClient? Client => _client;
    public static bool IsLoggedIn => _client is not null && !string.IsNullOrWhiteSpace(Session.AccessToken);
    public static Guid? SelectedProjectId => Session.SelectedProjectId;

    public static event Action? SessionChanged;

    public static void Reload()
    {
        Session = Store.Load();
        if (string.IsNullOrWhiteSpace(Session.ApiBaseUrl))
            Session.ApiBaseUrl = Store.DefaultApiBaseUrl;
    }

    public static void NotifyChanged() => SessionChanged?.Invoke();

    public static async Task ReloadRemoteCatalogAsync()
    {
        if (_client is null)
            return;
        await LoadRemoteCatalogAsync().ConfigureAwait(false);
    }

    private static async Task LoadRemoteCatalogAsync()
    {
        if (_client is null)
            return;

        var loaded = await ElementIdentityCatalogLoader.TryLoadFromApiAsync(_client).ConfigureAwait(false);
        if (loaded)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                PrefabPalette.RefreshCatalogCombos();
                PrefabPalette.TryRefresh();
            });
        }
    }

    public static async Task<bool> RestoreSessionAsync()
    {
        Reload();
        _client?.Dispose();
        _client = null;

        if (string.IsNullOrWhiteSpace(Session.AccessToken))
            return false;

        try
        {
            _client = new PrecastApiClient(Session.ApiBaseUrl, Session.AccessToken);
            var me = await _client.GetMeAsync().ConfigureAwait(false);
            Session.UserFullName = me.FullName;
            Session.Email = me.Email;
            Store.Save(Session);
            BimPrefabLog.Info($"Oturum geri yüklendi: {me.Email}");
            await LoadRemoteCatalogAsync().ConfigureAwait(false);
            NotifyChanged();
            return true;
        }
        catch (PrecastApiException)
        {
            try
            {
                var refreshed = await _client!.RefreshTokenAsync().ConfigureAwait(false);
                Session.AccessToken = refreshed.AccessToken;
                Store.Save(Session);
                await LoadRemoteCatalogAsync().ConfigureAwait(false);
                NotifyChanged();
                return true;
            }
            catch
            {
                ClearSession();
                return false;
            }
        }
    }

    public static async Task LoginAsync(string apiUrl, string email, string password)
    {
        _client?.Dispose();
        _client = new PrecastApiClient(apiUrl);
        await _client.TestConnectionAsync().ConfigureAwait(false);
        var login = await _client.LoginAsync(email, password).ConfigureAwait(false);
        Session.ApiBaseUrl = apiUrl.Trim().TrimEnd('/');
        Session.Email = login.Email;
        Session.UserFullName = login.FullName;
        Session.AccessToken = login.AccessToken;
        Store.Save(Session);
        await LoadRemoteCatalogAsync().ConfigureAwait(false);
        NotifyChanged();
    }

    public static void ClearSession()
    {
        _client?.Dispose();
        _client = null;
        AttributeCatalogService.ClearRemoteCatalog();
        Session.AccessToken = null;
        Session.UserFullName = null;
        Session.SelectedProjectId = null;
        Session.SelectedProjectLabel = null;
        Store.Save(Session);
        NotifyChanged();
    }

    public static void SetSelectedProject(Guid id, string label)
    {
        Session.SelectedProjectId = id;
        Session.SelectedProjectLabel = label;
        Store.Save(Session);
        NotifyChanged();
    }

    public static bool ShowLoginDialog(Window? owner = null)
    {
        var dlg = new PrecastFlowLoginWindow { Owner = owner ?? System.Windows.Application.Current?.MainWindow };
        return dlg.ShowDialog() == true;
    }

    public static SyncConflictChoice ResolveConflict(string productLabel)
    {
        var dlg = new SyncConflictWindow(productLabel) { Owner = System.Windows.Application.Current?.MainWindow };
        return dlg.ShowDialog() == true ? dlg.Choice : SyncConflictChoice.Skip;
    }

    public static async Task EnsureLoggedInAsync(Window? owner)
    {
        Reload();
        if (IsLoggedIn)
            return;

        if (await RestoreSessionAsync().ConfigureAwait(true))
            return;

        ShowLoginDialog(owner);
    }
}
