<#
  scrcpy-launcher.ps1
  Launcher interaktif untuk scrcpy di Windows.

  Pemakaian:
    .\scrcpy-launcher.ps1                              # menu interaktif
    .\scrcpy-launcher.ps1 -Mode record                 # langsung ke mode tertentu
    .\scrcpy-launcher.ps1 -Mode wifi-ip -Ip 192.168.1.20
    .\scrcpy-launcher.ps1 -Mode pair                   # pair via Wireless debugging lalu mirror
    .\scrcpy-launcher.ps1 -Mode normal -- --max-size=1280   # argumen tambahan ke scrcpy

  Mode: normal, view, screenoff, record, wifi, wifi-ip, lite, pair
#>
[CmdletBinding()]
param(
    [ValidateSet('normal', 'view', 'screenoff', 'record', 'wifi', 'wifi-ip', 'lite', 'pair')]
    [string]$Mode,
    [string]$Ip,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

# 'Continue': di Windows PowerShell 5.1, 'Stop' membuat output stderr adb
# (mis. "* daemon not running; starting now") dianggap error fatal.
$ErrorActionPreference = 'Continue'

# ---------- Helper output ----------
function Write-Warn([string]$m) { Write-Host "[!] $m" -ForegroundColor Yellow }
function Write-Err([string]$m)  { Write-Host "[x] $m" -ForegroundColor Red }
function Write-Ok([string]$m)   { Write-Host "[v] $m" -ForegroundColor Green }

# ---------- Cari scrcpy & adb ----------
# Folder dasar: folder .exe (diset oleh scrcpy-launcher.exe) atau folder script ini
$BaseDir = $PSScriptRoot
if ($env:SCRCPY_LAUNCHER_DIR -and (Test-Path $env:SCRCPY_LAUNCHER_DIR)) { $BaseDir = $env:SCRCPY_LAUNCHER_DIR }
$LocalDirs = @($BaseDir, (Join-Path $BaseDir 'scrcpy'))

# Salinan lokal (portable) diutamakan, baru yang terpasang di PATH
function Resolve-Tool([string]$name, [string[]]$dirs) {
    foreach ($d in $dirs) {
        if (-not $d) { continue }
        $p = Join-Path $d "$name.exe"
        if (Test-Path $p) { return $p }
    }
    $cmd = Get-Command $name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) { return $cmd.Source }
    return $null
}

$script:Scrcpy = Resolve-Tool 'scrcpy' $LocalDirs
if (-not $script:Scrcpy) {
    Write-Err 'scrcpy tidak ditemukan.'
    Write-Host '    Install dengan: winget install Genymobile.scrcpy'
    Write-Host '    atau taruh launcher ini di folder hasil ekstrak scrcpy.'
    Read-Host 'Tekan Enter untuk menutup' | Out-Null
    exit 1
}

# Ikuti symlink (winget memasang scrcpy sebagai link) supaya adb bawaannya ketemu
$real = $script:Scrcpy
try {
    $it = Get-Item $script:Scrcpy -ErrorAction Stop
    if ($it.LinkType -and $it.Target) {
        $t = @($it.Target)[0]
        if (-not [IO.Path]::IsPathRooted($t)) { $t = Join-Path $it.DirectoryName $t }
        $real = $t
    }
} catch { }
$scrcpyDir = Split-Path $real -Parent

# Utamakan adb bawaan scrcpy agar versinya cocok
$script:Adb = $null
foreach ($d in @($scrcpyDir) + $LocalDirs) {
    $p = Join-Path $d 'adb.exe'
    if (Test-Path $p) { $script:Adb = $p; break }
}
if (-not $script:Adb) { $script:Adb = Resolve-Tool 'adb' @() }
if (-not $script:Adb) {
    Write-Err 'adb tidak ditemukan. Gunakan paket scrcpy resmi (sudah termasuk adb).'
    Read-Host 'Tekan Enter untuk menutup' | Out-Null
    exit 1
}
$env:ADB = $script:Adb   # scrcpy akan memakai adb yang sama

# ---------- Penyimpanan IP terakhir ----------
$StateFile = Join-Path $env:LOCALAPPDATA 'scrcpy-launcher\last-ip.txt'
function Get-LastIp {
    if (Test-Path $StateFile) { return (Get-Content $StateFile -Raw).Trim() }
    return $null
}
function Save-LastIp([string]$v) {
    New-Item -ItemType Directory -Force -Path (Split-Path $StateFile) | Out-Null
    Set-Content -Path $StateFile -Value $v
}

# ---------- ADB ----------
function Get-AdbDevices {
    $out = & $script:Adb devices 2>$null
    $list = @()
    foreach ($line in $out) {
        if ($line -match '^(\S+)\s+(device|unauthorized|offline|no permissions|recovery|sideload)\b') {
            $list += [pscustomobject]@{ Serial = $Matches[1]; State = $Matches[2] }
        }
    }
    return $list
}

function Get-DeviceModel([string]$serial) {
    try {
        $m = & $script:Adb -s $serial shell getprop ro.product.model 2>$null
        if ($m) { return ($m | Out-String).Trim() }
    } catch { }
    return $serial
}

function Show-NoDeviceTips {
    Write-Host '    - Aktifkan Opsi Pengembang dan USB debugging di HP'
    Write-Host '    - Pakai kabel DATA (bukan kabel charge saja), coba port USB lain'
    Write-Host '    - Ubah mode USB di HP ke "Transfer file"'
    Write-Host '    - Xiaomi/POCO: aktifkan juga "USB debugging (Setelan keamanan)"'
    Write-Host '    - Tanpa kabel (Android 11+): tekan P untuk pair via Wireless debugging'
}

# ---------- Pair via Wireless debugging (Android 11+) ----------
function Invoke-WirelessPair {
    Write-Host ''
    Write-Host '------ Pair via Wireless debugging (Android 11+) ------' -ForegroundColor Cyan
    Write-Host ' Di HP: Opsi pengembang > Debug nirkabel (Wireless debugging) > ON,'
    Write-Host '        lalu ketuk "Sambungkan perangkat dengan kode penyambungan".'
    Write-Host ' HP dan PC harus di jaringan Wi-Fi yang sama. Biarkan dialog kode tetap terbuka.'
    Write-Host ''

    $pairAddr = Read-Host 'IP & port penyambungan (contoh 192.168.1.20:37099)'
    if (-not $pairAddr) { return $null }
    $pairAddr = $pairAddr.Trim()
    if ($pairAddr -notmatch '^[\w\.\-]+:\d+$') {
        Write-Err 'Format harus IP:port, sesuai yang tampil di dialog HP.'
        return $null
    }

    $code = Read-Host 'Kode penyambungan (6 digit)'
    if (-not $code) { return $null }
    $code = $code -replace '\s', ''
    if ($code -notmatch '^\d{6}$') { Write-Err 'Kode harus 6 digit.'; return $null }

    $out = (& $script:Adb pair $pairAddr $code 2>&1 | Out-String).Trim()
    if ($out -notmatch 'Successfully paired') {
        Write-Err "Pairing gagal: $out"
        Write-Host '    Kode cepat kedaluwarsa dan port penyambungan berubah tiap dialog dibuka.'
        Write-Host '    Buka ulang dialognya di HP lalu coba lagi.'
        return $null
    }
    Write-Ok 'Pairing berhasil.'
    $hostIp = $pairAddr.Split(':')[0]

    # adb sering langsung tersambung otomatis lewat mDNS setelah pairing
    Write-Host '    Menunggu koneksi otomatis...'
    for ($i = 0; $i -lt 5; $i++) {
        Start-Sleep -Seconds 1
        $d = @(Get-AdbDevices | Where-Object {
            $_.State -eq 'device' -and ($_.Serial -like "${hostIp}:*" -or $_.Serial -like 'adb-*')
        })
        if ($d.Count -gt 0) {
            Write-Ok "Tersambung: $($d[0].Serial)"
            return $d[0].Serial
        }
    }

    # Kalau belum, sambung manual ke port koneksi
    Write-Host ''
    Write-Host ' Lihat "Alamat IP & port" di layar utama Debug nirkabel.'
    Write-Host ' Port-nya BERBEDA dengan port penyambungan tadi.'
    $p = Read-Host "Port koneksi untuk $hostIp (contoh 41235)"
    if (-not $p) { return $null }
    $p = $p.Trim()
    if ($p -match ':') { $connAddr = $p } else { $connAddr = "${hostIp}:$p" }

    $out = (& $script:Adb connect $connAddr 2>&1 | Out-String).Trim()
    if ($out -match 'connected to') {
        Save-LastIp $connAddr
        Write-Ok "Tersambung: $connAddr"
        return $connAddr
    }
    Write-Err "Gagal connect: $out"
    return $null
}

function Select-Device {
    while ($true) {
        $devs   = @(Get-AdbDevices)
        $ready  = @($devs | Where-Object { $_.State -eq 'device' })
        $unauth = @($devs | Where-Object { $_.State -eq 'unauthorized' })
        $offline = @($devs | Where-Object { $_.State -eq 'offline' })

        if ($ready.Count -eq 1) { return $ready[0].Serial }

        if ($ready.Count -gt 1) {
            Write-Host ''
            Write-Host 'Beberapa device terhubung:'
            for ($i = 0; $i -lt $ready.Count; $i++) {
                $s = $ready[$i].Serial
                $kind = if ($s -match ':') { 'Wi-Fi' } else { 'USB' }
                Write-Host ("  {0}. {1}  ({2}, {3})" -f ($i + 1), (Get-DeviceModel $s), $s, $kind)
            }
            $sel = Read-Host 'Pilih nomor device'
            $n = 0
            if ([int]::TryParse($sel, [ref]$n) -and $n -ge 1 -and $n -le $ready.Count) {
                return $ready[$n - 1].Serial
            }
            Write-Warn 'Pilihan tidak valid.'
            continue
        }

        Write-Host ''
        if ($unauth.Count -gt 0) {
            Write-Warn "Device $($unauth[0].Serial) belum diizinkan."
            Write-Host '    Buka layar HP, centang "Selalu izinkan", lalu tekan "Izinkan".'
        } elseif ($offline.Count -gt 0) {
            Write-Warn "Device $($offline[0].Serial) offline. Coba cabut-colok kabel atau tekan R."
        } else {
            Write-Warn 'Tidak ada device terdeteksi.'
            Show-NoDeviceTips
        }

        $ans = Read-Host 'Enter = cek lagi | P = pair wireless | R = restart ADB | Q = batal'
        if ($null -eq $ans) { $ans = '' }
        switch ($ans.Trim().ToUpper()) {
            'Q' { return $null }
            'P' {
                $s = Invoke-WirelessPair
                if ($s) { return $s }
            }
            'R' {
                & $script:Adb kill-server 2>$null | Out-Null
                & $script:Adb start-server 2>$null | Out-Null
                Write-Ok 'ADB di-restart.'
            }
        }
    }
}

# ---------- Jalankan mode ----------
$script:LastCode = 0

function Invoke-Mode([string]$m) {
    $scArgs = @()

    if ($m -eq 'wifi-ip') {
        $target = $Ip
        if (-not $target) {
            $last = Get-LastIp
            $prompt = 'IP HP (contoh 192.168.1.20)'
            if ($last) { $prompt += " [Enter = $last]" }
            $target = Read-Host $prompt
            if (-not $target) { $target = $last }
        }
        if (-not $target) { Write-Err 'IP tidak diisi.'; $script:LastCode = 1; return }
        if ($target -notmatch ':\d+$') { $target += ':5555' }
        Save-LastIp $target
        $scArgs += "--tcpip=$target"
    } else {
        if ($m -eq 'pair') { $serial = Invoke-WirelessPair } else { $serial = Select-Device }
        if (-not $serial) { $script:LastCode = 1; return }
        $model = Get-DeviceModel $serial
        Write-Ok "Device: $model ($serial)"
        $scArgs += "--serial=$serial"
        $scArgs += "--window-title=scrcpy - $model"
    }

    switch ($m) {
        'view'      { $scArgs += '--no-control' }
        'screenoff' { $scArgs += '--turn-screen-off', '--stay-awake' }
        'record' {
            $dir = Join-Path ([Environment]::GetFolderPath('MyVideos')) 'scrcpy'
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
            $file = Join-Path $dir ("scrcpy_{0}.mp4" -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
            $scArgs += "--record=$file"
            Write-Ok "Rekaman akan disimpan ke: $file"
        }
        'wifi' {
            $scArgs += '--tcpip'
            Write-Host '    HP dan laptop harus di jaringan Wi-Fi yang sama.'
            Write-Host '    Setelah tersambung, kabel USB boleh dicabut.'
        }
        'lite' { $scArgs += '--max-size=1024', '--video-bit-rate=4M', '--max-fps=30' }
    }

    if ($ExtraArgs) { $scArgs += $ExtraArgs }

    Write-Host ("> scrcpy " + ($scArgs -join ' ')) -ForegroundColor DarkGray
    & $script:Scrcpy @scArgs
    $script:LastCode = $LASTEXITCODE

    if ($script:LastCode -ne 0) {
        Write-Warn "scrcpy berhenti dengan kode $($script:LastCode)."
        if ($m -like 'wifi*') {
            Write-Host '    Untuk Wi-Fi: pastikan satu jaringan, dan coba mode 5 (via USB) dulu.'
        }
    }
}

function Show-Menu {
    Write-Host ''
    Write-Host '=========== scrcpy launcher ===========' -ForegroundColor Cyan
    Write-Host ' 1. Normal         (mirror + kontrol)'
    Write-Host ' 2. View only      (mirror tanpa kontrol)'
    Write-Host ' 3. Layar HP mati  (hemat baterai, tetap tampil di PC)'
    Write-Host ' 4. Rekam layar    (simpan ke Videos\scrcpy)'
    Write-Host ' 5. Wi-Fi via USB  (aktifkan nirkabel dari kabel)'
    Write-Host ' 6. Wi-Fi via IP   (sambung ke IP yang sudah aktif)'
    Write-Host ' 7. Lite           (1024px, 4 Mbps, 30 fps)'
    Write-Host ' 8. Pair wireless  (Android 11+, tanpa kabel sama sekali)'
    Write-Host ' Q. Keluar'
    Write-Host '======================================='
}

# ---------- Main ----------
if ($Mode) {
    Invoke-Mode $Mode
    if ($script:LastCode -ne 0) { Read-Host 'Tekan Enter untuk menutup' | Out-Null }
    exit $script:LastCode
}

$map = @{
    '1' = 'normal'; '2' = 'view'; '3' = 'screenoff'; '4' = 'record'
    '5' = 'wifi';   '6' = 'wifi-ip'; '7' = 'lite'; '8' = 'pair'
}

while ($true) {
    Show-Menu
    $c = Read-Host 'Pilih'
    if ($null -eq $c) { $c = '' }
    $c = $c.Trim().ToUpper()
    if ($c -eq 'Q') { break }
    if ($map.ContainsKey($c)) { Invoke-Mode $map[$c] }
    else { Write-Warn 'Pilihan tidak valid.' }
}
