using System.Windows;
using BimPrefabExport.Core;
using BimPrefabExport.Services;

namespace BimPrefabExport.UI;

internal static class BomImportUiHelper
{
    private const string FileFilter = "CSV|*.csv|Excel|*.xlsx;*.xls|Tüm dosyalar|*.*";

    public static void TryAppendMaterialsFromFile(Window owner, ICollection<MaterialLine> target)
    {
        if (!TryPickRows(owner, out var rows))
            return;

        var preview = new TabularImportPreviewWindow(BomImportKind.Materials, rows)
        {
            Owner = owner,
        };
        if (preview.ShowDialog() != true || preview.AcceptedMaterials is null)
            return;

        foreach (var m in preview.AcceptedMaterials)
        {
            target.Add(new MaterialLine
            {
                Category = m.Category ?? "",
                Code = m.Code ?? "",
                Description = m.Description ?? "",
                Quantity = m.Quantity,
                Unit = m.Unit ?? "",
                Notes = m.Notes ?? "",
            });
        }
    }

    public static void TryAppendRebarsFromFile(Window owner, ICollection<RebarLine> target)
    {
        if (!TryPickRows(owner, out var rows))
            return;

        var preview = new TabularImportPreviewWindow(BomImportKind.Rebars, rows)
        {
            Owner = owner,
        };
        if (preview.ShowDialog() != true || preview.AcceptedRebars is null)
            return;

        foreach (var r in preview.AcceptedRebars)
        {
            target.Add(new RebarLine
            {
                PozNo = r.PozNo ?? "",
                DiameterMm = r.DiameterMm,
                Count = r.Count,
                LengthH_mm = r.LengthH_mm,
                LengthL_mm = r.LengthL_mm,
                Notes = r.Notes ?? "",
            });
        }
    }

    private static bool TryPickRows(Window owner, out List<string[]> rows)
    {
        rows = new List<string[]>();
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = FileFilter,
            FilterIndex = 1,
            Title = "CSV veya Excel tablosu seçin",
        };

        if (dlg.ShowDialog(owner) != true || string.IsNullOrWhiteSpace(dlg.FileName))
            return false;

        if (!ExcelSheetRowReader.TryReadFirstSheet(dlg.FileName, out rows, out var err))
        {
            System.Windows.MessageBox.Show(
                string.IsNullOrWhiteSpace(err) ? "Dosya okunamadı." : err,
                "BIM Prefab",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            rows = new List<string[]>();
            return false;
        }

        if (rows.Count == 0)
        {
            System.Windows.MessageBox.Show("Dosyada veri satırı bulunamadı.", "BIM Prefab", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        if (!BomTabularImportValidation.TryValidateParsedRows(rows, out var verr))
        {
            System.Windows.MessageBox.Show(
                string.IsNullOrWhiteSpace(verr) ? "Dosya doğrulanamadı." : verr,
                "BIM Prefab",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            rows = new List<string[]>();
            return false;
        }

        return true;
    }
}
