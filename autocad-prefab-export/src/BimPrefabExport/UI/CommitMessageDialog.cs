using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BimPrefabExport.UI;

internal static class CommitMessageDialog
{
    public static bool TryPrompt(
        Window owner,
        IReadOnlyList<string> changeLines,
        out string message)
    {
        message = "";

        var dlg = new Window
        {
            Title = "Sunucuya gönder",
            Width = 480,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF6, 0xF9)),
        };

        var messageBox = new System.Windows.Controls.TextBox
        {
            Margin = new Thickness(0, 4, 0, 8),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 72,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var summary = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x32, 0x32, 0x32)),
            Margin = new Thickness(0, 0, 0, 8),
            Text = BuildSummaryText(changeLines),
        };

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var intro = new TextBlock
        {
            Text = "Değişiklik özeti (git commit gibi bir mesaj girin):",
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x12, 0x3A, 0x5C)),
            FontWeight = FontWeights.SemiBold,
        };
        grid.Children.Add(intro);
        Grid.SetRow(summary, 1);
        grid.Children.Add(summary);
        var msgLabel = new TextBlock
        {
            Text = "Commit mesajı",
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x12, 0x3A, 0x5C)),
            Margin = new Thickness(0, 8, 0, 0),
        };
        Grid.SetRow(msgLabel, 2);
        grid.Children.Add(msgLabel);
        Grid.SetRow(messageBox, 3);
        grid.Children.Add(messageBox);

        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(16, 0, 16, 16),
        };
        var ok = new System.Windows.Controls.Button
        {
            Content = "Gönder",
            Width = 90,
            Height = 28,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true,
        };
        var cancel = new System.Windows.Controls.Button { Content = "İptal", Width = 70, Height = 28, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new StackPanel();
        root.Children.Add(grid);
        root.Children.Add(buttons);
        dlg.Content = root;

        var accepted = false;
        var resultMessage = "";
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(messageBox.Text))
                return;
            resultMessage = messageBox.Text.Trim();
            accepted = true;
            dlg.DialogResult = true;
            dlg.Close();
        };
        cancel.Click += (_, _) =>
        {
            dlg.DialogResult = false;
            dlg.Close();
        };

        dlg.ShowDialog();
        if (accepted)
            message = resultMessage;
        return accepted;
    }

    private static string BuildSummaryText(IReadOnlyList<string> changeLines)
    {
        if (changeLines.Count == 0)
            return "Gönderilecek değişiklik yok.";

        var sb = new StringBuilder();
        foreach (var line in changeLines.Take(12))
            sb.Append("• ").AppendLine(line);
        if (changeLines.Count > 12)
            sb.Append("• … ve ").Append(changeLines.Count - 12).AppendLine(" ürün daha");
        return sb.ToString().TrimEnd();
    }
}
