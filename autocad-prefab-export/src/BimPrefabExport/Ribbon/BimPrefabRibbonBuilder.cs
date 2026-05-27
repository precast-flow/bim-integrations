using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Windows;

namespace BimPrefabExport.Ribbon;

internal static class BimPrefabRibbonBuilder
{
    /// <summary>Yapı değişince artırın; aksi halde eski ribbon sekmesi önbellekte kalır.</summary>
    private const string TabId = "BIM_PREFAB_TAB_V6";

    public static void EnsureTab()
    {
        var ribbon = ComponentManager.Ribbon;
        if (ribbon is null)
            return;

        foreach (RibbonTab existing in ribbon.Tabs)
        {
            if (string.Equals(existing.Id, TabId, StringComparison.Ordinal))
                return;
        }

        var tab = new RibbonTab
        {
            Title = "BIM Prefab",
            Id = TabId,
        };

        var palettePanel = new RibbonPanelSource { Title = "Palet" };
        palettePanel.Items.Add(Large("Palet", "BIM_PREFAB_PANEL", "Ürün paletini açar (diğer komutlar palet içindedir).", RibbonIconFactory.Palette()));
        tab.Panels.Add(new RibbonPanel { Source = palettePanel });

        var drawingsPanel = new RibbonPanelSource { Title = "Çizimler" };
        drawingsPanel.Items.Add(Large(
            "Genel ve detay çizimler",
            "BIM_PREFAB_SHARED_DRAWINGS",
            "Projeye ortak çizimler ekleyin; isim verin, tek polyline sınırı atayın; PDF’yi ayrı klasöre alın.",
            RibbonIconFactory.SharedDrawings()));
        drawingsPanel.Items.Add(Large(
            "Ortak çizim PDF",
            "BIM_PREFAB_SHARED_DRAWINGS_PDF",
            "Çit tanımlı ortak çizimleri seçtiğiniz klasöre PDF olarak yazar.",
            RibbonIconFactory.PdfBulk()));
        tab.Panels.Add(new RibbonPanel { Source = drawingsPanel });

        ribbon.Tabs.Add(tab);
    }

    private static RibbonButton Large(string text, string command, string hint, (ImageSource Large, ImageSource Small) icons)
    {
        var b = new RibbonButton
        {
            Text = text,
            ShowText = true,
            Size = RibbonItemSize.Large,
            Orientation = System.Windows.Controls.Orientation.Vertical,
            CommandHandler = new RibbonAcadCommand(command),
            LargeImage = icons.Large,
            Image = icons.Small,
        };
        b.ToolTip = new RibbonToolTip { Title = text, Content = hint };
        return b;
    }
}
