using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BimPrefabExport.UI;

internal static class SimplePromptDialog
{
    public static bool TryPrompt(
        Window owner,
        string title,
        string codeLabel,
        string nameLabel,
        out string code,
        out string name)
    {
        code = "";
        name = "";

        var dlg = new Window
        {
            Title = title,
            Width = 380,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF6, 0xF9)),
        };

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var codeBox = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 4, 0, 8), Text = "PROJ-001" };
        var nameBox = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 4, 0, 8) };

        grid.Children.Add(new TextBlock
        {
            Text = codeLabel,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x12, 0x3A, 0x5C)),
        });
        Grid.SetRow(codeBox, 1);
        grid.Children.Add(codeBox);
        var nameLabelBlock = new TextBlock
        {
            Text = nameLabel,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x12, 0x3A, 0x5C)),
            Margin = new Thickness(0, 4, 0, 0),
        };
        Grid.SetRow(nameLabelBlock, 2);
        grid.Children.Add(nameLabelBlock);
        Grid.SetRow(nameBox, 3);
        grid.Children.Add(nameBox);

        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var ok = new System.Windows.Controls.Button { Content = "Oluştur", Width = 90, Height = 28, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new System.Windows.Controls.Button { Content = "İptal", Width = 70, Height = 28, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new StackPanel();
        root.Children.Add(grid);
        root.Children.Add(buttons);
        dlg.Content = root;

        var accepted = false;
        var resultCode = "";
        var resultName = "";
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(codeBox.Text) || string.IsNullOrWhiteSpace(nameBox.Text))
                return;
            resultCode = codeBox.Text.Trim();
            resultName = nameBox.Text.Trim();
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
        {
            code = resultCode;
            name = resultName;
        }

        return accepted;
    }
}
