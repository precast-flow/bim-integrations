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

**Her kod değişikliğinden sonra** Release derlemesini çalıştırın (WPF/WinForms belirsizlik hataları ve derleme kırılmalarını erken yakalamak için).

Build/clean betikleri **plugin kökünde** (`bim-integrations/autocad-prefab-export/scripts/`). `src/BimPrefabExport` içinde çalışıyorsanız aynı komutlar için yerel `scripts/` sarmalayıcıları da vardır.

### Plugin kökünden (`bim-integrations/autocad-prefab-export`)

```powershell
.\scripts\build.ps1
```

```bash
./scripts/build.sh
```

### Proje dizininden (`src/BimPrefabExport`)

```bash
dotnet build -c Release -p:Platform=x64
```

```powershell
.\scripts\build.ps1
```

```bash
./scripts/build.sh
```

(Alternatif: `..\..\scripts\build.ps1` veya `../../scripts/build.sh`)

**Temiz derleme (duplicate CS0579 / CS0101 hatalarından sonra):**

Plugin kökü:

```powershell
.\scripts\clean.ps1
.\scripts\build.ps1
```

```bash
./scripts/clean.sh && ./scripts/build.sh
```

`src/BimPrefabExport` içinden:

```powershell
.\scripts\clean.ps1
.\scripts\build.ps1
```

```bash
./scripts/clean.sh && ./scripts/build.sh
```

**Parallels (Mac paylaşımlı klasör):** `C:\Mac\...` altında `obj`/`bin` yazma izni sorunları olabilir. Proje otomatik olarak tüm ara dosyaları (WPF `_wpftmp` dahil) tek köke yazar: `%LOCALAPPDATA%\BimPrefabExport\build\`. Çıktı DLL: `%LOCALAPPDATA%\BimPrefabExport\build\bin\x64\Release\net10.0-windows\BimPrefabExport.dll`

Zorla yönlendirme: `dotnet build -p:BimPrefabRedirectBuildOutput=true`

Çıktı (doğrudan Windows diskinde build): `bin/x64/Release/net10.0-windows/BimPrefabExport.dll`  
Kilit için alternatif: `-p:OutputPath=...\artifacts\Release\net10.0-windows\`

## Yükleme (AutoCAD)

1. **`BimPrefabExport.dll`** dosyasını NETLOAD edin (tek DLL; ek NuGet bağımlılığı yok).
2. **BIM Prefab** → **Palet**

## Teknik komut adları

`BIM_PREFAB_PANEL`, `BIM_PREFAB_RECT_POLY`, `BIM_PREFAB_SHOW_PRODUCT`, `BIM_PREFAB_EXPORT_PDF_SINGLE`, `BIM_PREFAB_EXPORT_PDF_BULK`, `BIM_PREFAB_EXPORT_EXCEL` (CSV çifti), `BIM_PREFAB_EXPORT_BUNDLE` (klasör paketi), `BIM_PREFAB_EXPORT_MANIFEST`

## Paket sözleşmesi

- Şema: [schemas/manifest.v1.json](schemas/manifest.v1.json)
- Örnek: [samples/example-manifest.json](samples/example-manifest.json)

## PrecastFlow sunucu senkronu

Ribbon: **PrecastFlow'a bağlan** veya palet → **Giriş yap…** (modal login penceresi).

### Ağ (Parallels)

- Mac'te API: `dotnet run --launch-profile http` (`0.0.0.0:5255`)
- Windows AutoCAD varsayılan API: `http://10.211.55.2:5255` (Parallels Mac host)
- Override: `PRECASTFLOW_API_URL` veya `PRECASTFLOW_PARALLELS_HOST`

### Giriş

- **Endpoint:** `POST {API}/api/auth/login`
- **Seed kullanıcı:** `admin@precastflow.local` / `ChangeMe123!`
- **Seed proje:** `DEMO-001 — Demo Prefab Projesi`
- Oturum: `%LOCALAPPDATA%\BimPrefabExport\session.dat` (DPAPI)

### Sync akışı

| Aksiyon | Davranış |
|---------|----------|
| **Kaydet** | DWG + otomatik sunucuya push (giriş + proje seçiliyse) |
| **Sunucuya gönder** | Tüm çizim ürünlerini toplu push |
| **Sunucudan güncelle** | Sunucu ürünlerini DWG registry ile birleştir |
| Proje seçimi | Otomatik pull + merge |

Çakışmada modal: Sunucuyu kullan / Yereli kullan / Atla.

PDF: `POST /api/bim/projects/{projectId}/products/{productId}/pdf` (Kaydet/push sonrası).

### Eleman kimlik kataloğu

Tipoloji / eleman tipi / boyut alanları **gömülü JSON içermez**. Giriş sonrası OData katalog (`ElementIdentityCatalogLoader`) ve firma tipoloji ayarları API üzerinden yüklenir. Katalog yüklenmeden Tipoloji sekmesi devre dışı kalır.

### Manuel test

1. Backend Mac'te çalışsın; AutoCAD VM'de API = Mac IP
2. NETLOAD → **PrecastFlow'a bağlan** → giriş → `DEMO-001` seç
3. Ürün oluştur → **Kaydet** → `project_products` tablosunu kontrol et
4. **Sunucudan güncelle** ile web'den eklenen ürünlerin geldiğini doğrula
