using System.Windows;
using BimPrefabExport.Services;

namespace BimPrefabExport.UI;

public partial class SyncConflictWindow : Window
{
    public SyncConflictChoice Choice { get; private set; } = SyncConflictChoice.Skip;

    public SyncConflictWindow(string productLabel)
    {
        InitializeComponent();
        MessageText.Text = $"«{productLabel}» için veri çakışması algılandı.";
    }

    private void Finish(SyncConflictChoice choice)
    {
        Choice = choice;
        DialogResult = true;
        Close();
    }

    private void OnUseServer(object sender, RoutedEventArgs e) => Finish(SyncConflictChoice.UseServer);
    private void OnUseLocal(object sender, RoutedEventArgs e) => Finish(SyncConflictChoice.UseLocal);
    private void OnSkip(object sender, RoutedEventArgs e) => Finish(SyncConflictChoice.Skip);
}
