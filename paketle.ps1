# Aktarım paketi: dist\VideoHat-Paketi (VideoHat + Kdenlive + BASLAT.bat) ve 7z arşivi üretir.
# Önce hazirla.ps1 çalışmış, _indir\kdenlive-*_standalone.exe indirilmiş olmalı.
param(
    [string]$SevenZip = 'C:\Program Files\7-Zip\7z.exe',
    [int]$Sikistirma = 5,
    [switch]$SadeceKlasor,   # klasörü kur, sıkıştırma
    [switch]$KlasorHazir,    # klasör zaten kurulu: yalnız sıkıştır
    [string]$ParcaBoyutu = ''    # örn. 3900m (FAT32 USB için bölünmüş arşiv); boş = tek dosya
)
$ErrorActionPreference = 'Stop'
$proje = $PSScriptRoot
$gpu = Join-Path $proje 'dist\VideoHat-GPU'
$cikis = Join-Path $proje 'dist\VideoHat-Paketi'
$kdExe = Get-ChildItem (Join-Path $proje '_indir') -Filter 'kdenlive-*_standalone.exe' | Sort-Object Name | Select-Object -Last 1
if (-not (Test-Path $gpu)) { throw "GPU paketi yok: $gpu (önce hazirla.ps1)" }
if (-not $kdExe) { throw "Kdenlive standalone .exe _indir\ içinde yok." }
if (-not (Test-Path $SevenZip)) { throw "7z bulunamadı: $SevenZip" }

if (-not $KlasorHazir) {
Write-Host '[1/5] Klasör hazırlanıyor...'
if (Test-Path $cikis) { Remove-Item $cikis -Recurse -Force }
New-Item -ItemType Directory $cikis | Out-Null
$vh = Join-Path $cikis 'VideoHat'
robocopy $gpu $vh /E /NFL /NDL /NJH /NJS /NP /XD (Join-Path $gpu 'is') | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy hatası (kod $LASTEXITCODE)" }
foreach ($d in 'gelen', 'isleniyor', 'bitti', 'hata', 'gunluk') { New-Item -ItemType Directory -Force (Join-Path $vh "is\$d") | Out-Null }
Copy-Item (Join-Path $proje 'kaynak\KULLANIM.txt') $vh -Force
New-Item -ItemType Directory -Force (Join-Path $vh 'araclar') | Out-Null
Copy-Item (Join-Path $proje 'kaynak\baslat\baslat.ps1') (Join-Path $vh 'araclar') -Force
Copy-Item (Join-Path $proje 'kaynak\baslat\BASLAT.bat') $cikis -Force
Copy-Item (Join-Path $proje 'kaynak\baslat\OKU-BENI.txt') $cikis -Force
$surucu = Get-ChildItem (Join-Path $proje '_indir') -Filter '*-desktop-win10-win11-64bit-*-whql.exe' | Sort-Object Name | Select-Object -Last 1
if ($surucu) {
    $sd = Join-Path $cikis 'Surucu'
    New-Item -ItemType Directory $sd | Out-Null
    Copy-Item $surucu.FullName $sd
    Copy-Item (Join-Path $proje 'kaynak\baslat\SURUCU-OKU-BENI.txt') $sd -Force
} else { Write-Host 'UYARI: NVIDIA sürücüsü _indir\ içinde yok, pakete eklenmedi.' -ForegroundColor Yellow }

Write-Host '[2/5] Kdenlive açılıyor...'
$kd = Join-Path $cikis 'Kdenlive'
& $SevenZip x $kdExe.FullName "-o$kd" -y -bso0 -bsp0
if ($LASTEXITCODE -ne 0) { throw 'Kdenlive açılamadı' }

Write-Host '[3/5] Doğrulama (dosya sayısı/boyut)...'
$boyut = (Get-ChildItem $cikis -Recurse -File | Measure-Object Length -Sum).Sum / 1GB
"{0:N2} GB, {1} dosya" -f $boyut, (Get-ChildItem $cikis -Recurse -File).Count

}
if ($SadeceKlasor) { Write-Host "Klasör hazır: $cikis"; return }
Write-Host '[4/5] 7z sıkıştırılıyor (birkaç dakika-yarım saat)...'
$arsiv = Join-Path $proje 'dist\VideoHat-Paketi.7z'
Get-ChildItem (Join-Path $proje 'dist') -Filter 'VideoHat-Paketi.7z*' | Remove-Item -Force
$a = @('a', '-t7z', "-mx=$Sikistirma", '-mmt=on', '-bso0', '-bsp0', $arsiv, $cikis)
if ($ParcaBoyutu) { $a += "-v$ParcaBoyutu" }
& $SevenZip @a
if ($LASTEXITCODE -ne 0) { throw '7z başarısız' }

Write-Host '[5/5] Arşiv bütünlüğü sınanıyor...'
$ilk = if ($ParcaBoyutu) { "$arsiv.001" } else { $arsiv }
& $SevenZip t $ilk -bso0 -bsp0
if ($LASTEXITCODE -ne 0) { throw '7z test başarısız' }
$h = (Get-FileHash $ilk -Algorithm SHA256).Hash
Get-ChildItem (Join-Path $proje 'dist') -Filter 'VideoHat-Paketi.7z*' | ForEach-Object { '{0}  {1:N2} GB' -f $_.Name, ($_.Length / 1GB) }
"SHA256 ($(Split-Path $ilk -Leaf)): $h"
Set-Content (Join-Path $proje 'dist\VideoHat-Paketi.sha256.txt') "$h  $(Split-Path $ilk -Leaf)" -Encoding ASCII
