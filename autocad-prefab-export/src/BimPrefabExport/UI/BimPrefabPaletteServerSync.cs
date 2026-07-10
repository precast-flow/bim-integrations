using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using BimPrefabExport.Core;
using BimPrefabExport.Services;

namespace BimPrefabExport.UI;

public partial class BimPrefabPaletteWindow
{
    private System.Windows.Controls.ComboBox? _projectCombo;
    private System.Windows.Controls.TextBox? _projectSearchBox;
    private ICollectionView? _projectCollectionView;
    private List<ProjectComboItem> _allProjectItems = [];
    private TextBlock? _connectionStatusText;
    private System.Windows.Controls.Button? _loginButton;
    private System.Windows.Controls.Button? _logoutButton;
    private System.Windows.Controls.Button? _syncButton;
    private System.Windows.Controls.Button? _pullButton;
    private System.Windows.Controls.Button? _refreshProjectsButton;
    private bool _serverUiBusy;

    private void BuildServerSyncPanel()
    {
        PrecastFlowSessionManager.Reload();
        PrecastFlowSessionManager.SessionChanged += OnPrecastSessionChanged;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "PrecastFlow sunucu",
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x12, 0x3A, 0x5C)),
            Margin = new Thickness(0, 0, 0, 6),
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var form = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _projectSearchBox = new System.Windows.Controls.TextBox
        {
            Margin = new Thickness(0, 2, 8, 2),
            MinWidth = 220,
        };
        _projectSearchBox.TextChanged += OnProjectSearchTextChanged;

        var searchLabel = new TextBlock
        {
            Text = "Proje ara",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 6, 8, 0),
        };
        Grid.SetRow(searchLabel, 0);
        Grid.SetColumn(searchLabel, 0);
        form.Children.Add(searchLabel);
        Grid.SetRow(_projectSearchBox, 0);
        Grid.SetColumn(_projectSearchBox, 1);
        form.Children.Add(_projectSearchBox);

        _projectCombo = new System.Windows.Controls.ComboBox
        {
            Margin = new Thickness(0, 2, 8, 2),
            IsEditable = false,
            DisplayMemberPath = "Label",
            SelectedValuePath = "Id",
            MinWidth = 220,
        };
        _projectCombo.SelectionChanged += OnProjectComboSelectionChanged;

        var projectLabel = new TextBlock
        {
            Text = "Proje",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 6, 8, 0),
        };
        Grid.SetRow(projectLabel, 1);
        Grid.SetColumn(projectLabel, 0);
        form.Children.Add(projectLabel);
        Grid.SetRow(_projectCombo, 1);
        Grid.SetColumn(_projectCombo, 1);
        form.Children.Add(_projectCombo);

        Grid.SetRow(form, 1);
        root.Children.Add(form);

        var bottom = new StackPanel();
        var actions = new WrapPanel();
        _loginButton = CreateToolbarButton("Giriş yap…", null, OnOpenLoginClick);
        _logoutButton = CreateToolbarButton("Çıkış", null, OnServerLogoutClick);
        _refreshProjectsButton = CreateToolbarButton("Projeleri yenile", null, OnRefreshProjectsClick);
        _pullButton = CreateToolbarButton("Sunucudan güncelle", null, OnPullFromServerClick);
        _syncButton = CreateToolbarButton("Sunucuya gönder", PaletteWpfIcon.FromBitmap(PaletteIcons.Save), OnPushToServerClick);
        actions.Children.Add(_loginButton);
        actions.Children.Add(_logoutButton);
        actions.Children.Add(_refreshProjectsButton);
        actions.Children.Add(_pullButton);
        actions.Children.Add(_syncButton);
        bottom.Children.Add(actions);

        _connectionStatusText = new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x32, 0x32, 0x32)),
        };
        bottom.Children.Add(_connectionStatusText);

        Grid.SetRow(bottom, 2);
        root.Children.Add(bottom);

        ServerSyncPanelHost.Child = root;
        UpdateServerConnectionUi();
        _ = InitializePrecastSessionAsync();
    }

    private void OnProjectSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _projectCollectionView?.Refresh();
    }

    private bool FilterProjectItems(object item)
    {
        if (item is not ProjectComboItem project)
            return false;

        var q = _projectSearchBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(q))
            return true;

        return project.Label.Contains(q, StringComparison.CurrentCultureIgnoreCase);
    }

    private void OnPrecastSessionChanged()
    {
        Dispatcher.Invoke(() =>
        {
            UpdateServerConnectionUi();
            RefreshCatalogDropdowns();
        });
    }

    private async Task InitializePrecastSessionAsync()
    {
        if (await PrecastFlowSessionManager.RestoreSessionAsync().ConfigureAwait(true))
        {
            await LoadProjectsIntoComboAsync(selectSaved: true).ConfigureAwait(true);
            SetServerStatus($"Giriş yapıldı: {PrecastFlowSessionManager.Session.UserFullName}");
            return;
        }

        SetServerStatus("Sunucuya bağlanmak için «Giriş yap…» kullanın.");
        await Dispatcher.InvokeAsync(() =>
        {
            if (!PrecastFlowSessionManager.IsLoggedIn)
                PrecastFlowSessionManager.ShowLoginDialog(this);
            if (PrecastFlowSessionManager.IsLoggedIn)
                _ = LoadProjectsIntoComboAsync(selectSaved: true);
        });
    }

    private void OnOpenLoginClick(object sender, RoutedEventArgs e)
    {
        if (PrecastFlowSessionManager.ShowLoginDialog(this))
        {
            _ = LoadProjectsIntoComboAsync(selectSaved: false);
            SetServerStatus($"Giriş başarılı: {PrecastFlowSessionManager.Session.UserFullName}");
        }
    }

    private void OnServerLogoutClick(object sender, RoutedEventArgs e)
    {
        PrecastFlowSessionManager.ClearSession();
        _projectCombo!.ItemsSource = null;
        _allProjectItems = [];
        SetServerStatus("Çıkış yapıldı.");
        UpdateServerConnectionUi();
    }

    private async void OnRefreshProjectsClick(object sender, RoutedEventArgs e)
    {
        if (_serverUiBusy || !PrecastFlowSessionManager.IsLoggedIn)
            return;

        try
        {
            SetServerUiBusy(true, "Projeler yükleniyor…");
            await LoadProjectsIntoComboAsync(selectSaved: true).ConfigureAwait(true);
            SetServerStatus("Proje listesi güncellendi.");
        }
        catch (Exception ex)
        {
            SetServerStatus(ex.Message, isError: true);
        }
        finally
        {
            SetServerUiBusy(false);
        }
    }

    private async void OnPullFromServerClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedProject(out var projectId))
            return;

        try
        {
            SetServerUiBusy(true, "Sunucudan güncelleniyor…");
            var result = await ProjectProductSyncService.PullAndMergeAsync(
                PrecastFlowSessionManager.Client!,
                projectId,
                PrecastFlowSessionManager.ResolveConflict).ConfigureAwait(true);

            RefreshFromActiveDocument();
            var summary =
                $"Sunucudan: {result.Pulled} güncellendi/eklendi, {result.Conflicts} çakışma, {result.Skipped} atlandı.";
            SetServerStatus(summary, isError: result.Failed > 0);
            StatusText.Text = summary;
        }
        catch (Exception ex)
        {
            SetServerStatus(ex.Message, isError: true);
        }
        finally
        {
            SetServerUiBusy(false);
        }
    }

    private async void OnPushToServerClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedProject(out var projectId))
            return;

        TrySaveCurrentProductLocally(showErrors: true);

        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            SetServerStatus("Aktif çizim yok.", isError: true);
            return;
        }

        IReadOnlyList<ProductRecord> localProducts;
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            localProducts = new RegistryService().ListProducts(tr, doc.Database);
            tr.Commit();
        }

        var dirty = localProducts.Where(ProductDirtyTracker.IsDirty).ToList();
        if (dirty.Count == 0)
        {
            SetServerStatus("Gönderilecek değişiklik yok.", isError: true);
            return;
        }

        var changeLines = dirty
            .Select(p =>
            {
                var s = ProductDirtyTracker.BuildChangeSummary(p);
                return $"{s.ProductCode}: {s.SummaryText}";
            })
            .ToList();

        if (!CommitMessageDialog.TryPrompt(this, changeLines, out var commitMessage))
            return;

        try
        {
            SetServerUiBusy(true, "Değişiklikler sunucuya gönderiliyor…");
            var progress = new Progress<string>(msg => SetServerStatus(msg));
            var result = await ProjectProductSyncService.PushDirtyProductsWithCommitAsync(
                PrecastFlowSessionManager.Client!,
                projectId,
                commitMessage,
                progress).ConfigureAwait(true);

            RefreshFromActiveDocument();
            var summary =
                $"Commit tamamlandı: {result.Created} yeni, {result.Updated} güncellendi, {result.Skipped} atlandı, {result.Failed} hata.";
            if (result.Warnings.Count > 0)
                summary += " " + string.Join(" | ", result.Warnings.Take(2));
            if (result.Errors.Count > 0)
                summary += " " + string.Join(" | ", result.Errors.Take(3));
            SetServerStatus(summary, isError: result.Failed > 0);
            StatusText.Text = summary;
        }
        catch (Exception ex)
        {
            SetServerStatus(ex.Message, isError: true);
        }
        finally
        {
            SetServerUiBusy(false);
        }
    }

    private async void OnProjectComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_projectCombo?.SelectedItem is not ProjectComboItem item || item.Id == Guid.Empty)
            return;

        PrecastFlowSessionManager.SetSelectedProject(item.Id, item.Label);
        UpdateServerConnectionUi();

        if (_serverUiBusy || !PrecastFlowSessionManager.IsLoggedIn)
            return;

        try
        {
            SetServerUiBusy(true, "Proje ürünleri sunucudan alınıyor…");
            var result = await ProjectProductSyncService.PullAndMergeAsync(
                PrecastFlowSessionManager.Client!,
                item.Id,
                PrecastFlowSessionManager.ResolveConflict).ConfigureAwait(true);
            RefreshFromActiveDocument();
            SetServerStatus($"Proje seçildi — sunucudan {result.Pulled} ürün birleştirildi.");
        }
        catch (Exception ex)
        {
            SetServerStatus(ex.Message, isError: true);
        }
        finally
        {
            SetServerUiBusy(false);
        }
    }

    private bool TryGetSelectedProject(out Guid projectId)
    {
        projectId = Guid.Empty;
        if (!PrecastFlowSessionManager.IsLoggedIn || PrecastFlowSessionManager.Client is null)
        {
            SetServerStatus("Önce giriş yapın.", isError: true);
            PrecastFlowSessionManager.ShowLoginDialog(this);
            return false;
        }

        if (_projectCombo?.SelectedItem is not ProjectComboItem item || item.Id == Guid.Empty)
        {
            SetServerStatus("Göndermek için bir proje seçin.", isError: true);
            return false;
        }

        projectId = item.Id;
        return true;
    }

    private async Task LoadProjectsIntoComboAsync(bool selectSaved)
    {
        if (PrecastFlowSessionManager.Client is null)
            return;

        var projects = await PrecastFlowSessionManager.Client.ListProjectsAsync().ConfigureAwait(true);
        _allProjectItems = projects
            .Select(p => new ProjectComboItem(p.Id, $"{p.Code} — {p.Name}"))
            .OrderBy(p => p.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _projectCollectionView = CollectionViewSource.GetDefaultView(_allProjectItems);
        _projectCollectionView.Filter = FilterProjectItems;
        _projectCombo!.ItemsSource = _projectCollectionView;

        if (selectSaved && PrecastFlowSessionManager.Session.SelectedProjectId is Guid saved && saved != Guid.Empty)
            SelectProjectInCombo(saved);
        else if (_projectCombo.SelectedItem is null && _allProjectItems.Count > 0)
            _projectCombo.SelectedIndex = 0;
    }

    private void SelectProjectInCombo(Guid projectId)
    {
        var match = _allProjectItems.FirstOrDefault(i => i.Id == projectId);
        if (match is not null)
            _projectCombo!.SelectedItem = match;
    }

    private void UpdateServerConnectionUi()
    {
        var loggedIn = PrecastFlowSessionManager.IsLoggedIn;
        var projectSelected = _projectCombo?.SelectedItem is ProjectComboItem item && item.Id != Guid.Empty;

        if (_loginButton is not null)
            _loginButton.IsEnabled = !_serverUiBusy;
        if (_logoutButton is not null)
            _logoutButton.IsEnabled = !_serverUiBusy && loggedIn;
        if (_refreshProjectsButton is not null)
            _refreshProjectsButton.IsEnabled = !_serverUiBusy && loggedIn;
        if (_pullButton is not null)
            _pullButton.IsEnabled = !_serverUiBusy && loggedIn && projectSelected;
        if (_syncButton is not null)
            _syncButton.IsEnabled = !_serverUiBusy && loggedIn && projectSelected;
        if (_projectCombo is not null)
            _projectCombo.IsEnabled = !_serverUiBusy && loggedIn;
        if (_projectSearchBox is not null)
            _projectSearchBox.IsEnabled = !_serverUiBusy && loggedIn;

        if (loggedIn && _connectionStatusText is not null
            && string.IsNullOrWhiteSpace(_connectionStatusText.Text))
        {
            SetServerStatus(
                $"Bağlı: {PrecastFlowSessionManager.Session.UserFullName} · API: {PrecastFlowSessionManager.Session.ApiBaseUrl}");
        }
    }

    private void SetServerUiBusy(bool busy, string? status = null)
    {
        _serverUiBusy = busy;
        if (!string.IsNullOrWhiteSpace(status))
            SetServerStatus(status);
        UpdateServerConnectionUi();
    }

    private void SetServerStatus(string message, bool isError = false)
    {
        if (_connectionStatusText is null)
            return;
        _connectionStatusText.Text = message;
        _connectionStatusText.Foreground = new SolidColorBrush(
            isError ? System.Windows.Media.Color.FromRgb(0xB0, 0x00, 0x20) : System.Windows.Media.Color.FromRgb(0x32, 0x32, 0x32));
        BimPrefabLog.Info($"[Sunucu] {message}");
    }

    private sealed record ProjectComboItem(Guid Id, string Label);
}
