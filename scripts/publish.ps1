<#
.SYNOPSIS
  Publish Mirraawwrring sebagai aplikasi portable satu exe (PROMPT.md §8).

.DESCRIPTION
  1. Membaca versi dari Directory.Build.props.
  2. (Opsional) menjalankan unit test.
  3. dotnet publish win-x64 self-contained single-file ke dist\Mirraawwrring-<versi>\.
  4. -BundleScrcpy: mengunduh scrcpy sesuai config\scrcpy-manifest.json, memverifikasi SHA-256
     (gagal = berhenti, file dihapus), mengekstrak ke dist\Mirraawwrring-<versi>\scrcpy\, dan Unblock-File.
  5. Membuat dist\Mirraawwrring-<versi>-win-x64.zip.

.EXAMPLE
  .\scripts\publish.ps1
  .\scripts\publish.ps1 -BundleScrcpy
  .\scripts\publish.ps1 -BundleScrcpy -SkipTests -NoZip
#>
[CmdletBinding()]
param(
    [switch]$BundleScrcpy,
    [switch]$SkipTests,
    [switch]$NoZip,
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repo = Split-Path -Parent $PSScriptRoot
$props = Join-Path $repo 'Directory.Build.props'
$manifestPath = Join-Path $repo 'config\scrcpy-manifest.json'
$project = Join-Path $repo 'src\Mirraawwrring.App\Mirraawwrring.App.csproj'
$solution = Join-Path $repo 'Mirraawwrring.sln'

# ---- versi dari Directory.Build.props ----
[xml]$propsXml = Get-Content $props
$version = ($propsXml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw "Elemen <Version> tidak ditemukan di $props" }
$version = $version.Trim()

$distRoot = Join-Path $repo 'dist'
$outName = "Mirraawwrring-$version"
$outDir = Join-Path $distRoot $outName
$zipPath = Join-Path $distRoot "$outName-$Runtime.zip"

Write-Host "== Mirraawwrring $version ($Configuration, $Runtime) ==" -ForegroundColor Cyan

# ---- test ----
if (-not $SkipTests) {
    Write-Host '-- dotnet test' -ForegroundColor Cyan
    dotnet test $solution -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Unit test gagal; publish dibatalkan.' }
}

# ---- publish ----
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Path $outDir | Out-Null

Write-Host "-- dotnet publish -> $outDir" -ForegroundColor Cyan
dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -o $outDir --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish gagal.' }

$exe = Join-Path $outDir 'Mirraawwrring.exe'
if (-not (Test-Path $exe)) { throw "Exe tidak ditemukan: $exe" }
if (-not (Test-Path (Join-Path $outDir 'config\scrcpy-manifest.json'))) { throw 'config\scrcpy-manifest.json tidak ikut ter-publish.' }

Copy-Item (Join-Path $repo 'README.md') $outDir
Copy-Item (Join-Path $repo 'CHANGELOG.md') $outDir

# ---- bundel scrcpy (opsional) ----
if ($BundleScrcpy) {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $scrcpyDir = Join-Path $outDir 'scrcpy'
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("mirraawwrring-publish-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tmp | Out-Null
    $zipFile = Join-Path $tmp 'scrcpy.zip'
    try {
        Write-Host "-- mengunduh scrcpy $($manifest.version): $($manifest.url)" -ForegroundColor Cyan
        Invoke-WebRequest -Uri $manifest.url -OutFile $zipFile -UseBasicParsing

        $actual = (Get-FileHash $zipFile -Algorithm SHA256).Hash.ToLowerInvariant()
        $expected = ([string]$manifest.sha256).ToLowerInvariant()
        if ($actual -ne $expected) {
            Remove-Item $zipFile -Force
            throw "SHA-256 tidak cocok. Diharapkan $expected, didapat $actual. Unduhan dihapus; scrcpy TIDAK dibundel."
        }
        Write-Host "   SHA-256 cocok ($actual)" -ForegroundColor Green

        $extract = Join-Path $tmp 'extract'
        Expand-Archive -Path $zipFile -DestinationPath $extract -Force
        $source = Join-Path $extract $manifest.folderInZip
        if (-not (Test-Path (Join-Path $source 'scrcpy.exe'))) { throw "Folder $($manifest.folderInZip) di dalam zip tidak berisi scrcpy.exe." }
        if (Test-Path $scrcpyDir) { Remove-Item $scrcpyDir -Recurse -Force }
        Move-Item $source $scrcpyDir
        foreach ($required in 'scrcpy.exe', 'adb.exe', 'scrcpy-server') {
            if (-not (Test-Path (Join-Path $scrcpyDir $required))) { throw "File $required hilang di bundel scrcpy." }
        }
        Get-ChildItem $scrcpyDir -Recurse -File | Unblock-File
        Write-Host "   scrcpy dibundel ke $scrcpyDir" -ForegroundColor Green
    }
    finally {
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ---- zip ----
if (-not $NoZip) {
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Write-Host "-- zip -> $zipPath" -ForegroundColor Cyan
    Compress-Archive -Path $outDir -DestinationPath $zipPath -CompressionLevel Optimal
}

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ''
Write-Host "Selesai: $exe ($size MB)" -ForegroundColor Green
if (-not $NoZip) { Write-Host "         $zipPath ($([math]::Round((Get-Item $zipPath).Length / 1MB, 1)) MB)" -ForegroundColor Green }
if (-not $BundleScrcpy) { Write-Host 'Catatan: scrcpy tidak dibundel; aplikasi akan menawarkan unduhan lewat wizard setup. Pakai -BundleScrcpy untuk paket lengkap.' }
