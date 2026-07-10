using System.Windows.Controls;
using BimPrefabExport.Core;
using BimPrefabExport.Services;

namespace BimPrefabExport.UI;

internal static class BomGridSetup
{
    public static void ConfigureMaterialsGrid(System.Windows.Controls.DataGrid dg, MaterialCatalogService catalog)
    {
        dg.Columns.Clear();

        var catalogCol = new DataGridComboBoxColumn
        {
            Header = "Katalog",
            Width = new DataGridLength(2, DataGridLengthUnitType.Star),
            MinWidth = 140,
            ItemsSource = catalog.CatalogPickerOptions,
            SelectedValuePath = "Code",
            DisplayMemberPath = "DisplayLabel",
            SelectedValueBinding = new System.Windows.Data.Binding(nameof(MaterialLine.MaterialCatalogCode))
            {
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            },
        };
        dg.Columns.Add(catalogCol);

        dg.Columns.Add(EditableTextColumn("Kategori", nameof(MaterialLine.Category), 1.2));
        dg.Columns.Add(EditableTextColumn("Kod", nameof(MaterialLine.Code), 80));
        dg.Columns.Add(EditableTextColumn("Açıklama", nameof(MaterialLine.Description), 2));
        dg.Columns.Add(EditableTextColumn("Miktar", nameof(MaterialLine.Quantity), 72));
        dg.Columns.Add(EditableTextColumn("Birim", nameof(MaterialLine.Unit), 56));
        dg.Columns.Add(ReadOnlyNullableDoubleColumn("kg", nameof(MaterialLine.LineWeightKg), 64));
        dg.Columns.Add(EditableTextColumn("Not", nameof(MaterialLine.Notes), 1));
    }

    public static void ConfigureRebarsGrid(System.Windows.Controls.DataGrid dg, MaterialCatalogService catalog)
    {
        dg.Columns.Clear();

        dg.Columns.Add(new DataGridTextColumn
        {
            Header = "Poz",
            Binding = new System.Windows.Data.Binding(nameof(RebarLine.PozNo)) { Mode = System.Windows.Data.BindingMode.OneWay },
            Width = 48,
            IsReadOnly = true,
        });

        var diameterCol = new DataGridComboBoxColumn
        {
            Header = "Çap (mm)",
            Width = 88,
            ItemsSource = RebarWeightHelper.StandardDiametersMm,
            SelectedValueBinding = new System.Windows.Data.Binding(nameof(RebarLine.DiameterMm))
            {
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            },
        };
        dg.Columns.Add(diameterCol);

        var gradeCol = new DataGridComboBoxColumn
        {
            Header = "Sınıf",
            Width = 72,
            ItemsSource = catalog.SteelGrades,
            SelectedValueBinding = new System.Windows.Data.Binding(nameof(RebarLine.SteelGrade))
            {
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            },
        };
        dg.Columns.Add(gradeCol);

        var shapeCol = new DataGridComboBoxColumn
        {
            Header = "Şekil",
            Width = 88,
            ItemsSource = catalog.RebarShapes,
            SelectedValuePath = "Id",
            DisplayMemberPath = "Label",
            SelectedValueBinding = new System.Windows.Data.Binding(nameof(RebarLine.Shape))
            {
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            },
        };
        dg.Columns.Add(shapeCol);

        dg.Columns.Add(EditableNullableDoubleColumn("Boy (mm)", nameof(RebarLine.DevelopedLengthMm), 88));
        dg.Columns.Add(EditableTextColumn("Adet", nameof(RebarLine.Count), 72));
        dg.Columns.Add(ReadOnlyNullableDoubleColumn("kg", nameof(RebarLine.TotalWeightKg), 64));
        dg.Columns.Add(EditableTextColumn("Not", nameof(RebarLine.Notes), 1));
    }

    private static DataGridTextColumn EditableTextColumn(string header, string property, double widthOrStar)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new System.Windows.Data.Binding(property) { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged },
            Width = WidthFrom(widthOrStar),
        };
    }

    private static DataGridTextColumn ReadOnlyTextColumn(string header, string property, double widthOrStar)
    {
        var col = EditableTextColumn(header, property, widthOrStar);
        col.IsReadOnly = true;
        return col;
    }

    private static DataGridTextColumn EditableNullableDoubleColumn(string header, string property, double width)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new System.Windows.Data.Binding(property)
            {
                Converter = new NullableDoubleConverter(),
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            },
            Width = width,
        };
    }

    private static DataGridTextColumn ReadOnlyNullableDoubleColumn(string header, string property, double width)
    {
        var col = EditableNullableDoubleColumn(header, property, width);
        col.IsReadOnly = true;
        return col;
    }

    private static DataGridLength WidthFrom(double widthOrStar)
    {
        return widthOrStar >= 1 && widthOrStar <= 5
            ? new DataGridLength(widthOrStar, DataGridLengthUnitType.Star)
            : widthOrStar;
    }
}
