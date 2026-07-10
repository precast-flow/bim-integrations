# DWG dosyasini Parallels Mac klasorunden Windows yerel diskine kopyalar.
# PowerShell:
#   Set-ExecutionPolicy -Scope Process Bypass
#   & "C:\Mac\Home\Documents\projects\startups\precast-app\bim-integrations\autocad-prefab-export\scripts\copy-dwg-to-windows-local.ps1"

$ErrorActionPreference = "Stop"

function Find-SourceDwg {
    $candidates = @(
        "C:\Mac\Home\Desktop\*\*A33.007*.dwg",
        "C:\Mac\Home\Desktop\*A33.007*.dwg",
        "C:\Mac\Home\Desktop\Digital Nomad\Firmalar\KAMBETON\PLANRADAR SUNUM\*\*A33.007*.dwg"
    )

    foreach ($pattern in $candidates) {
        $match = Get-ChildItem -Path $pattern -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($match) {
            return $match.FullName
        }
    }

    return $null
}

$source = Find-SourceDwg
if (-not $source) {
    Write-Host "Kaynak DWG bulunamadi. Desktop uzerinde *A33.007*.dwg aranir."
    exit 1
}

$destDir = Join-Path $env:USERPROFILE "Documents\BimPrefabWork"
New-Item -ItemType Directory -Force -Path $destDir | Out-Null
$dest = Join-Path $destDir "CUKUROVA-KURUYEMIS-2022-A33.007.dwg"

Write-Host "Kaynak : $source"
Write-Host "Hedef  : $dest"
Write-Host ""

Copy-Item -LiteralPath $source -Destination $dest -Force

if (-not (Test-Path -LiteralPath $dest)) {
    Write-Error "Kopyalama basarisiz: hedef dosya yok."
}

$srcSize = (Get-Item -LiteralPath $source).Length
$dstSize = (Get-Item -LiteralPath $dest).Length
Write-Host "Kaynak boyut: $srcSize byte"
Write-Host "Hedef boyut: $dstSize byte"

if ($srcSize -ne $dstSize) {
    Write-Error "Boyutlar eslesmiyor, kopya eksik olabilir."
}

$item = Get-Item -LiteralPath $dest
$item.IsReadOnly = $false

$user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
& icacls $destDir /inheritance:e | Out-Null
& icacls $destDir /grant "${user}:(OI)(CI)F" /T | Out-Null

Write-Host ""
Write-Host "Basarili. AutoCAD icinde su dosyayi acin:"
Write-Host $dest
