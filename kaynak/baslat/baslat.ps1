# VideoHat başlatıcı menüsü (Windows 10/11, yerleşik PowerShell 5.1; kurulum gerektirmez).
# Etkileşimli: BASLAT.bat çift tıklanır. Otomatik test: -Dosya <yol> [-Agir e|h] [-Goruntu e|h]
param(
    [string]$Dosya = '',
    [string]$Agir = 'h',
    [string]$Goruntu = 'h'
)
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$kok = Split-Path -Parent $PSScriptRoot          # ...\VideoHat  (bu dosya araclar\ altında)
$paketKok = Split-Path -Parent $kok              # ...\VideoHat-Paketi
$kdenlive = Get-ChildItem (Join-Path $paketKok 'Kdenlive') -Recurse -Filter kdenlive.exe -ErrorAction SilentlyContinue | Select-Object -First 1
$env:VIDEOHAT_PAUSE = '0'

function Baslik {
    Clear-Host
    Write-Host '=============================================' -ForegroundColor Cyan
    Write-Host '  VideoHat  -  Türkçe transkript + ses/görüntü' -ForegroundColor Cyan
    Write-Host '=============================================' -ForegroundColor Cyan
    Write-Host ''
}

$surucuDizin = Join-Path $paketKok 'Surucu'
$surucuDosya = Get-ChildItem $surucuDizin -Filter '*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1

function Surucu-Surumu {
    $smi = Get-Command nvidia-smi -ErrorAction SilentlyContinue
    if (-not $smi) { return $null }
    $v = & nvidia-smi --query-gpu=driver_version --format=csv,noheader 2>$null | Select-Object -First 1
    if ($v) { try { return [version]($v.Trim()) } catch { return $null } }
    return $null
}

function Cihaz-Ozeti {
    $smi = Get-Command nvidia-smi -ErrorAction SilentlyContinue
    if ($smi) {
        $o = & nvidia-smi --query-gpu=name,memory.total,driver_version --format=csv,noheader 2>$null
        if ($o) {
            Write-Host "  Ekran kartı: $($o | Select-Object -First 1)" -ForegroundColor Gray
            $sv = Surucu-Surumu
            if ($sv -and $sv -lt [version]'528.33') {
                Write-Host "  UYARI: NVIDIA sürücüsü eski ($sv). GPU için en az 528.33 gerekir -> menüden 7 ile paketteki sürücüyü kurun." -ForegroundColor Yellow
            }
            return
        }
    }
    Write-Host '  Ekran kartı: NVIDIA bulunamadı -> işlemci (CPU) ile çalışır, daha yavaş olur.' -ForegroundColor Yellow
}

function Evet-Mi([string]$soru, [string]$varsayilan = 'h') {
    $c = Read-Host "$soru (e/h) [$varsayilan]"
    if ([string]::IsNullOrWhiteSpace($c)) { $c = $varsayilan }
    return $c.Trim().ToLower().StartsWith('e')
}

function Dosya-Sec {
    Add-Type -AssemblyName System.Windows.Forms
    $d = New-Object System.Windows.Forms.OpenFileDialog
    $d.Title = 'İşlenecek video/ses dosyalarını seçin (birden fazla seçilebilir)'
    $d.Filter = 'Video/ses|*.mp4;*.mkv;*.mov;*.avi;*.m4a;*.wav;*.mp3|Tüm dosyalar|*.*'
    $d.Multiselect = $true
    if ($d.ShowDialog() -eq 'OK') { return $d.FileNames } else { return @() }
}

function Klasor-Sec {
    Add-Type -AssemblyName System.Windows.Forms
    $d = New-Object System.Windows.Forms.FolderBrowserDialog
    $d.Description = 'İçindeki tüm videoların işleneceği klasörü seçin'
    if ($d.ShowDialog() -eq 'OK') { return @($d.SelectedPath) } else { return @() }
}

function Isle([string[]]$girdiler, [bool]$agir, [bool]$goruntu) {
    if (-not $girdiler -or $girdiler.Count -eq 0) { Write-Host 'Seçim yapılmadı.' -ForegroundColor Yellow; return }
    $args2 = @($girdiler)
    if ($agir) { $args2 += '--agir-ses' }
    if ($goruntu) { $args2 += '--goruntu' }
    Write-Host ''
    Write-Host 'İşleniyor... (pencereyi kapatmayın; uzun videolar dakikalar sürebilir)' -ForegroundColor Green
    & (Join-Path $kok 'calistir.bat') @args2
    $kod = $LASTEXITCODE
    Write-Host ''
    if ($kod -eq 0) { Write-Host 'BİTTİ. Çıktılar her videonun yanındaki "<ad>_videohat" klasöründe.' -ForegroundColor Green }
    else { Write-Host "Hata oluştu (kod $kod). Ayrıntı: $kok\is\gunluk" -ForegroundColor Red }
    $ilk = $girdiler[0]
    $klasor = if (Test-Path $ilk -PathType Container) { $ilk } else { Split-Path -Parent $ilk }
    if ($kod -eq 0 -and $klasor -and -not $Dosya) { Start-Process explorer.exe -ArgumentList ('"' + $klasor + '"') }
}

# Otomatik mod (test)
if ($Dosya) {
    Isle @($Dosya) ($Agir.ToLower().StartsWith('e')) ($Goruntu.ToLower().StartsWith('e'))
    exit $LASTEXITCODE
}

while ($true) {
    Baslik
    Cihaz-Ozeti
    Write-Host ''
    Write-Host '  1) Video/ses dosyası seç ve işle'
    Write-Host '  2) Klasör seç ve içindeki tümünü işle'
    Write-Host '  3) Klasör izleme: "gelen" klasörüne atılan videolar otomatik işlensin'
    Write-Host '  4) Kdenlive''i aç (video düzenleme)'
    Write-Host '  5) Kendi kendine test (ilk çalıştırmada önerilir)'
    Write-Host '  6) Kullanım kılavuzunu aç'
    Write-Host '  7) NVIDIA ekran kartı sürücüsünü kur (yalnız gerekirse; yönetici izni ister)'
    Write-Host '  0) Çıkış'
    Write-Host ''
    $s = Read-Host 'Seçiminiz'
    switch ($s) {
        '1' { $g = Dosya-Sec; if ($g.Count) { Isle $g (Evet-Mi 'Ağır gürültü giderme (çok gürültülü kayıt)?') (Evet-Mi 'Görüntü iyileştirme?') }; Read-Host 'Devam için Enter' | Out-Null }
        '2' { $g = Klasor-Sec; if ($g.Count) { Isle $g (Evet-Mi 'Ağır gürültü giderme (çok gürültülü kayıt)?') (Evet-Mi 'Görüntü iyileştirme?') }; Read-Host 'Devam için Enter' | Out-Null }
        '3' {
            $gelen = Join-Path $kok 'is\gelen'
            New-Item -ItemType Directory -Force $gelen | Out-Null
            Start-Process explorer.exe -ArgumentList ('"' + $gelen + '"')
            Write-Host "Videoları açılan 'gelen' klasörüne kopyalayın. İzleme penceresini kapatana kadar çalışır (Ctrl+C ile durur)." -ForegroundColor Green
            & (Join-Path $kok 'izle.bat')
        }
        '4' {
            if ($kdenlive) { Start-Process $kdenlive.FullName } else { Write-Host 'Kdenlive bulunamadı (VideoHat-Paketi\Kdenlive).' -ForegroundColor Red; Read-Host 'Enter' | Out-Null }
        }
        '5' { & (Join-Path $kok 'kontrol.bat'); Read-Host 'Devam için Enter' | Out-Null }
        '6' { Start-Process notepad.exe -ArgumentList ('"' + (Join-Path $kok 'KULLANIM.txt') + '"') }
        '7' {
            if (-not $surucuDosya) { Write-Host 'Pakette sürücü dosyası yok (VideoHat-Paketi\Surucu).' -ForegroundColor Red }
            else {
                $sv = Surucu-Surumu
                Write-Host "Paketteki sürücü: $($surucuDosya.Name)" -ForegroundColor Gray
                if ($sv) { Write-Host "Kurulu sürücü: $sv" -ForegroundColor Gray } else { Write-Host 'Kurulu NVIDIA sürücüsü bulunamadı.' -ForegroundColor Yellow }
                Write-Host 'Kurulum Windows''un yönetici onayını (UAC) ister ve ekranı birkaç kez yeniler; bittikten sonra bilgisayarı yeniden başlatın.' -ForegroundColor Yellow
                if (Evet-Mi 'Sürücü kurulum programı başlatılsın mı?' 'h') { Start-Process -FilePath $surucuDosya.FullName }
            }
            Read-Host 'Devam için Enter' | Out-Null
        }
        '0' { exit 0 }
        default { }
    }
}
