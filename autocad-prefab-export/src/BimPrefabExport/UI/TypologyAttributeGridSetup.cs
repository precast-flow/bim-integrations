using System.Windows.Controls;

namespace BimPrefabExport.UI;

internal static class TypologyAttributeGridSetup
{
    public static void ConfigureValueColumns(System.Windows.Controls.DataGrid dg)
    {
        dg.Columns.Clear();
        dg.Columns.Add(new DataGridTextColumn
        {
            Header = "Alan",
            Binding = new System.Windows.Data.Binding("Label") { Mode = System.Windows.Data.BindingMode.OneWay },
            IsReadOnly = true,
            Width = new DataGridLength(2, DataGridLengthUnitType.Star),
        });
        dg.Columns.Add(new DataGridTextColumn
        {
            Header = "Değer",
            Binding = new System.Windows.Data.Binding("Value")
            {
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            },
            Width = new DataGridLength(3, DataGridLengthUnitType.Star),
        });
    }
}
