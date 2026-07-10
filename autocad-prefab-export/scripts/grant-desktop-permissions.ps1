# Parallels paylasimli Mac Desktop icin tam yazma izni.
# Windows PowerShell'i "Yonetici olarak calistir" ile acin, sonra:
#   Set-ExecutionPolicy -Scope Process Bypass
#   & "...\grant-desktop-permissions.ps1"
# Tek dosya icin:
#   & "...\grant-desktop-permissions.ps1" -FilePath "C:\Mac\Home\Desktop\dosya.dwg"

param(
    [string]$FilePath = ""
)

$ErrorActionPreference = "Stop"

$user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
Write-Host "Kullanici: $user"

function Grant-FullControl {
    param([string]$Path, [switch]$Recurse)

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Warning "Bulunamadi: $Path"
        return
    }

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        $f = Get-Item -LiteralPath $Path
        $f.IsReadOnly = $false
    }

    $args = @("/F", $Path)
    if ($Recurse) { $args += "/R" }
    & takeown @args /D Y | Out-Null

    $icaclsArgs = @($Path, "/inheritance:e")
    if ($Recurse) { $icaclsArgs += "/T" }
    & icacls @icaclsArgs | Out-Null

    $grantArgs = @($Path, "/grant", "${user}:(OI)(CI)F")
    if ($Recurse) { $grantArgs += "/T" }
    & icacls @grantArgs | Out-Null

    Write-Host "Tamam: $Path"
}

if ($FilePath -ne "") {
    Grant-FullControl -Path $FilePath
    exit 0
}

$paths = @(
    "$env:USERPROFILE\Desktop",
    "C:\Mac\Home\Desktop"
) | Where-Object { Test-Path $_ } | Select-Object -Unique

if ($paths.Count -eq 0) {
    Write-Error "Desktop klasoru bulunamadi."
}

Write-Host "Hedef:" ($paths -join ", ")
Write-Host ""

foreach ($path in $paths) {
    Write-Host "=== $path ==="

    Get-ChildItem -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { -not $_.PSIsContainer -and $_.IsReadOnly } |
        ForEach-Object { $_.IsReadOnly = $false }

    Grant-FullControl -Path $path -Recurse
}

Write-Host ""
Write-Host "Not: AutoCAD icin dosyayi Windows yerel diskine kopyalayin:"
Write-Host "  copy-dwg-to-windows-local.ps1"
