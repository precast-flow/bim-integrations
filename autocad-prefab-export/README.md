# AutoCAD prefab export (BIM Prefab)

AutoCAD için .NET eklentisi (bu repo dalı **net10.0-windows** + **x64**, AutoCAD 2025/2027 managed API ile uyumlu): çizimde **ürün registry** (NOD + XRecord) ve çizim nesnelerinde **XData** ile `productId` bağlantısı; **PDF export**; listeler için **CSV** (UTF-8 BOM, Excel’de açılabilir). İsteğe bağlı `manifest.json` komutu duruyor. Eski AutoCAD (.NET Framework) için ayrı `TargetFramework` / proje kopyası gerekir.

## Arayüz

- **Ribbon:** **BIM Prefab** sekmesinde yalnızca **Palet** (diğer komutlar yüzen panel içinde).
- **Palet:** **WPF** yüzen panel (~**640×780**); AutoCAD ana penceresine sahiplendirilir. **Tablo** görünümünde ürün listesi: **onay kutusu**, **Ürün / Kod / Rev**, **Çizim / PDF** sütunu (ör. `📄 2 adet çizim referansı` — polyline ile eklenen çit sayısı). Üstte **«Tüm ürünleri işaretle»** (CSV / paket için). **Ctrl/Shift** ile satır seçimi (polyline hedefi, silme, düzenleme). **Bağlantıları temizle**: işaretli veya seçili ürünlerin **XData bağlantıları** ve **kayıtlı polyline çitleri** silinir (yeniden polyline seçmek kullanıcıya kalmış). **Polyline sınır**: ardışık **birden fazla** kapalı polyline seçilebilir; iptal / Enter ile biter; komut bitince palet listesi yenilenir. **Ürün bilgisi** sekmesinde **PDF kağıt** (varsayılan **A3**). **Paket (CSV+PDF)…**: üst klasör seçilir, **`ÇizimAdı_BimPrefab_yyyyMMdd_HHmmss`** içinde **`…_urunler.csv`**, **`…_materyaller.csv`** ve alt klasör **`PDF`**. **Malzemeler / donatı** sekmesi.

## PDF export sorun giderme

1. **Eklenti log dosyası (önerilen):**  
   `%LOCALAPPDATA%\BimPrefabExport\bim-prefab.log`  
   Her PDF denemesinde plotter seçimi ve hata metinleri buraya yazılır. PDF başarısız olunca komut satırında da bu yol gösterilir.

   Plot çıktısı önce `%TEMP%` altında ASCII adlı geçici `.pdf` dosyasına yazılır, sonra hedefe kopyalanır (Türkçe karakterli yol sorunlarını azaltır). **BACKGROUNDPLOT** geçici olarak kapatılır; arka plan plot yüzünden dosyanın geç oluşması engellenir.

2. **AutoCAD komut satırı geçmişi:** **F2** (TEXTSCR) ile tam metin penceresi.

3. **AutoCAD oturum günlükleri (Autodesk):** Sürüme göre örnek:  
   `%LOCALAPPDATA%\Autodesk\AutoCAD\R25.0\enu\` veya `%PROGRAMDATA%\Autodesk\` altında hata/izleme dosyaları. Kurulum ve dil klasörü (`enu` / `tr`) farklı olabilir.

4. **Sık nedenler:** PDF plotter adı yerelleştirilmiş olabilir — log’da hangi `.pc3` adlarının denendiği görünür. **DWG To PDF** plotter’ı Plotter Yöneticisi’nde yoksa önce AutoCAD PDF çıktısını bir kez elle deneyin.

## Gereksinimler

- .NET SDK (build için)
- AutoCAD kurulumu (managed DLL referansları)
- `Directory.Build.props` içindeki `AcadInstallPath` veya:  
  `dotnet build -p:AcadInstallPath="C:\Program Files\Autodesk\AutoCAD 2022"`

## Derleme

```bash
cd src/BimPrefabExport
dotnet build -c Release -p:Platform=x64
```

Çıktı: `bin/x64/Release/net10.0-windows/BimPrefabExport.dll`  
Kilit için alternatif: `-p:OutputPath=...\artifacts\Release\net10.0-windows\`

## Yükleme (AutoCAD)

1. **`BimPrefabExport.dll`** dosyasını NETLOAD edin (tek DLL; ek NuGet bağımlılığı yok).
2. **BIM Prefab** → **Palet**

## Teknik komut adları

`BIM_PREFAB_PANEL`, `BIM_PREFAB_RECT_POLY`, `BIM_PREFAB_SHOW_PRODUCT`, `BIM_PREFAB_EXPORT_PDF_SINGLE`, `BIM_PREFAB_EXPORT_PDF_BULK`, `BIM_PREFAB_EXPORT_EXCEL` (CSV çifti), `BIM_PREFAB_EXPORT_BUNDLE` (klasör paketi), `BIM_PREFAB_EXPORT_MANIFEST`

## Paket sözleşmesi

- Şema: [schemas/manifest.v1.json](schemas/manifest.v1.json)
- Örnek: [samples/example-manifest.json](samples/example-manifest.json)
