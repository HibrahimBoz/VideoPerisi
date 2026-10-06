# Sentetik test verisi üretir (geliştirme makinesi): Microsoft Tolga (tr-TR) sesi + ffmpeg.
# Çıktı: dist\test_veri\ref.wav, temiz.mp4, gurultulu.mp4, yarim.mp4, ornek_tr.wav (5 sn)
param([string]$Ffmpeg = "")
$ErrorActionPreference = 'Stop'
$kok = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$cikis = Join-Path $kok 'dist\test_veri'
New-Item -ItemType Directory -Force $cikis | Out-Null
if (-not $Ffmpeg) {
    $Ffmpeg = (Get-ChildItem (Join-Path $kok 'dist') -Recurse -Filter ffmpeg.exe | Select-Object -First 1).FullName
}
if (-not $Ffmpeg) { throw "ffmpeg.exe bulunamadı; -Ffmpeg ile yol verin." }

Add-Type -AssemblyName System.Speech
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
$ses = $s.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Culture.Name -eq 'tr-TR' } | Select-Object -First 1
if (-not $ses) { throw "tr-TR sesi kurulu değil." }
$s.SelectVoice($ses.VoiceInfo.Name)
$s.Rate = 0
$metin = Get-Content (Join-Path $PSScriptRoot 'metin_tr.txt') -Raw -Encoding UTF8
$ref = Join-Path $cikis 'ref.wav'
$s.SetOutputToWaveFile($ref); $s.Speak($metin); $s.Dispose()
Write-Host "Ses: $($ses.VoiceInfo.Name) → $ref"

# Süre
$sure = [double](& $Ffmpeg -i $ref 2>&1 | Select-String 'Duration: (\d+):(\d+):([\d.]+)' | ForEach-Object {
    [double]$_.Matches[0].Groups[1].Value * 3600 + [double]$_.Matches[0].Groups[2].Value * 60 + [double]$_.Matches[0].Groups[3].Value })

$yazi = "Türev ve İntegral"
$font = 'C\:/Windows/Fonts/arial.ttf'
$temiz = Join-Path $cikis 'temiz.mp4'
& $Ffmpeg -y -loglevel error -f lavfi -i "color=c=white:s=1280x720:r=15:d=$sure" -i $ref `
    -vf "drawtext=fontfile='$font':text='$yazi':fontsize=64:x=(w-text_w)/2:y=(h-text_h)/2" `
    -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest $temiz
if ($LASTEXITCODE) { throw "temiz.mp4 üretilemedi" }

# Gürültülü: pembe gürültü ~10 dB SNR (konuşma ~ -20 dBFS, gürültü ~ -30)
$gur = Join-Path $cikis 'gurultulu.mp4'
& $Ffmpeg -y -loglevel error -i $temiz -f lavfi -i "anoisesrc=color=pink:amplitude=0.05:d=$sure" `
    -filter_complex "[0:a][1:a]amix=inputs=2:duration=first:normalize=0[a]" -map 0:v -map "[a]" `
    -c:v copy -c:a aac $gur
if ($LASTEXITCODE) { throw "gurultulu.mp4 üretilemedi" }

# Yarım dosya: ilk %50 bayt
$yarim = Join-Path $cikis 'yarim.mp4'
$bayt = [System.IO.File]::ReadAllBytes($temiz)
[System.IO.File]::WriteAllBytes($yarim, $bayt[0..([int]($bayt.Length / 2))])

# 5 sn örnek (pakete gömülecek): metnin ilk cümleleri
$s2 = New-Object System.Speech.Synthesis.SpeechSynthesizer
$s2.SelectVoice($ses.VoiceInfo.Name)
$ornek = Join-Path $cikis 'ornek_tr.wav'
$ornekMetin = "Merhaba arkadaşlar, bugünkü dersimizde türev kavramını işleyeceğiz."
$s2.SetOutputToWaveFile($ornek); $s2.Speak($ornekMetin); $s2.Dispose()
Set-Content -Path (Join-Path $cikis 'ornek_tr.txt') -Value $ornekMetin -Encoding UTF8
Copy-Item (Join-Path $PSScriptRoot 'metin_tr.txt') (Join-Path $cikis 'metin_tr.txt') -Force
Write-Host "Tamam: $cikis"
