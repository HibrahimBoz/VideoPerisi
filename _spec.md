# VideoHat modül sözleşmesi (TÜM yazarlar bu imzalara uyar)

Paket: `C:\Users\Halil\Documents\VideoHat\kaynak\app\videohat\`. Python 3.12 (embeddable, yalnız stdlib + faster-whisper/ctranslate2/av/onnxruntime/tokenizers/huggingface-hub). Başka paket YOK (numpy faster-whisper ile gelir; kullanma gerekmedikçe). Yorum/mesaj Türkçe. Mevcut dosyalar (DEĞİŞTİRME): `hatalar.py`, `yollar.py`, `ayar.py`, `__init__.py`, `__main__.py`.

## Mevcut, kullanılacak API
- `hatalar`: `VideoHatHatasi`, `AyarHatasi`, `OrtamHatasi`, `IsHatasi`, `UpscaleHatasi`
- `yollar`: `kok()`, `ffmpeg()`, `ffprobe()`, `model_dizini(ad)->Path` (model.bin yoksa OrtamHatasi), `model_var_mi(ad)->bool`, `deep_filter()`, `realesrgan()`, `is_dizini(alt)`
- `ayar`: `Ayar` dataclass: `.kok`, `.stt` (gpu_model, cpu_model, beam_size, vad, terimler_dosyasi), `.ses` (highpass_hz, afftdn_nf, lufs, tp, lra, agir_kuru_oran), `.goruntu` (etkin, crf, nvenc), `.izle`, `.genel` (ara_dosya_sakla, cpu_threads)

## cihaz.py
```python
@dataclass
class CihazSecimi:
    device: str          # "cuda" | "cpu"
    compute_type: str    # "float16" | "int8_float16" | "int8"
    model_ad: str        # "large-v3" | "medium" | "turbo"
    neden: str           # Türkçe kısa gerekçe (günlüğe)
def gpu_bilgi() -> list[dict] | None      # nvidia-smi --query-gpu=memory.free,name --format=csv,noheader,nounits ; yoksa/hata None; dict: {"ad":str,"bos_mb":int}
def sec(ayar: Ayar, zorla_cihaz: str|None=None, zorla_model: str|None=None) -> CihazSecimi
```
Kural: (1) `zorla_cihaz=="cpu"` → CPU. (2) cuda: `import ctranslate2; ctranslate2.get_cuda_device_count()==0` (import/DLL hatası dahil her istisna) → CPU. (3) CUDA var: boş bellek (nvidia-smi, ilk GPU): ≥5500 → ayar.stt.gpu_model + float16; 3000–5499 → gpu_model + int8_float16; 1800–2999 → "medium" + int8_float16; <1800 veya nvidia-smi okunamadı(None) → CPU (neden: bellek yetersiz/okunamadı). Seçilen modelin `yollar.model_var_mi` değilse sıradaki küçük modele düş (large-v3→medium), hiçbiri yoksa `OrtamHatasi`. (4) CPU: `ayar.stt.cpu_model`, compute_type "int8"; model yoksa başka bulunan modele düş. (5) `zorla_model` (large-v3|medium|turbo) verilmişse model adını ona sabitle (compute_type kuralı aynı kalır); model dizini yoksa OrtamHatasi. Her dönüşte `neden` dolu.

## stt.py
```python
@dataclass
class Segment: bas: float; son: float; metin: str
def yaz(wav16: Path, secim: CihazSecimi, ayar: Ayar, terimler: str) -> list[Segment]
```
`WhisperModel(str(yollar.model_dizini(secim.model_ad)), device=secim.device, compute_type=secim.compute_type, cpu_threads=(ayar.genel.cpu_threads or fiziksel çekirdek ≈ os.cpu_count()//2 en az 1), num_workers=1)`. Model repo adı ASLA verilmez (yerel yol). `transcribe(str(wav16), language="tr", beam_size=ayar.stt.beam_size, vad_filter=ayar.stt.vad, vad_parameters={"min_silence_duration_ms":500}, condition_on_previous_text=False, initial_prompt=terimler or None, word_timestamps=False)`. Segment üreteci tüketilir (list). Boş/yalnız-boşluk metinleri at; metni `.strip()`. Hata sarmalama YOK (RuntimeError/OSError olduğu gibi yükselir; hat.py yakalar).

## ses.py  (ffmpeg: `yollar.ffmpeg()`, subprocess, `check=True`, stderr yakala, `creationflags=CREATE_NO_WINDOW` Windows'ta, hata → IsHatasi(Türkçe + ffmpeg son 5 satır))
```python
def cikar(video: Path, hedef_wav: Path, sr: int = 48000) -> None   # -vn -ac 1 -ar sr pcm_s16le
def temizle(wav_in: Path, wav_out: Path, agir: bool, ayar: Ayar) -> None
def stt_icin(wav48: Path, wav16: Path) -> None                     # 16 kHz mono pcm_s16le
def olc_lufs(wav: Path) -> float                                    # ebur128 Integrated LUFS (stderr ayrıştır); okunamazsa IsHatasi
```
`temizle` normal: `-af highpass=f={highpass_hz},afftdn=nf={afftdn_nf},loudnorm=I={lufs}:TP={tp}:LRA={lra}`. Ağır: önce `deep-filter.exe` (araç yolu `yollar.deep_filter()`; girdi 48 kHz mono wav; `-o <geçici dizin>`; çıktı dosyasını bul) sonra ham ile temiz çıktıyı `amix`/`volume` ile `agir_kuru_oran` kuru oranda karıştır (`[0]volume=1-oran[a];[1]volume=oran[b];[a][b]amix=inputs=2:normalize=0`), ardından aynı highpass+loudnorm. deep-filter yoksa/başarısızsa uyarı günlüğüne düş (`logging.getLogger("videohat")`) ve normal moda geç (hata fırlatma). Tek-geçiş loudnorm yeterli (iki geçiş YOK). Çıkış `wav_out` 48 kHz mono pcm_s16le.

## cikti.py
```python
def srt(seg: list[Segment], yol: Path) -> None
def vtt(seg: list[Segment], yol: Path) -> None
def txt(seg: list[Segment], yol: Path) -> None
def json_meta(seg: list[Segment], secim: CihazSecimi, sureler: dict[str,float], yol: Path) -> None
```
UTF-8 BOM'SUZ. SRT/VTT cue metni ≤42 karakter/satır, kelime sınırında en çok 2 satıra böl (daha uzunsa cue'yu zaman oranlı birden çok cue'ya böl). SRT zaman `HH:MM:SS,mmm`, VTT `HH:MM:SS.mmm` + `WEBVTT\n\n` başlığı. Zaman yuvarlama: `round(t*1000)` ms. json_meta: `{"cihaz","compute_type","model","neden","sureler","segment_sayisi","sure_sn"}` ensure_ascii=False.

## goruntu.py
```python
def iyilestir(video: Path, temiz_wav: Path, cikis: Path, ayar: Ayar, nvenc: bool) -> None
def upscale(video: Path, cikis: Path, ayar: Ayar) -> None
```
iyilestir: `-i video -i temiz_wav -map 0:v:0 -map 1:a:0 -vf "hqdn3d=1.5:1.5:6:6,eq=contrast=1.05:brightness=0.02:saturation=1.05,unsharp=5:5:0.5"`; nvenc True ve ffmpeg `-encoders`'ta h264_nvenc varsa VE `nvidia-smi` çalışıyorsa `-c:v h264_nvenc -cq {crf+1} -preset p5`, yoksa `-c:v libx264 -crf {crf} -preset medium`; `-c:a aac -b:a 192k -shortest -movflags +faststart -pix_fmt yuv420p`. upscale: kare PNG çıkar → `realesrgan-ncnn-vulkan -n realesrgan-x4plus -s 2` → yeniden birleştir (ses kopya); Vulkan/araç yoksa `UpscaleHatasi`; geçici kare dizini `cikis.parent/_ara/kareler_<ad>`, iş sonunda sil.

## hat.py
```python
@dataclass
class Bayraklar: agir_ses: bool=False; goruntu: bool=False; upscale: bool=False; model: str|None=None; cihaz: str|None=None
@dataclass
class Sonuc: ad: str; basarili: bool; hata: str|None; sureler: dict[str,float]; cikti_dosyalari: list[Path]; cihaz: str
def isle_tek(video: Path, cikis_dir: Path, ayar: Ayar, bayraklar: Bayraklar) -> Sonuc
```
Adımlar (her adım süresi `sureler[adim]` sn, `time.perf_counter`): `ses_cikar` (cikis_dir/_ara/ham48.wav) → `ses_temizle` (temiz48.wav) → `ses_16k` (temiz16.wav) → `cihaz_sec` → `stt` → `cikti` (`<ad>.srt/.vtt/.txt/.json`) → `mux` (ffmpeg: orijinal video `-c:v copy` + temiz ses AAC 192k → `<ad>_ses.mp4`, `-map 0:v:0 -map 1:a:0 -shortest`) → isteğe bağlı `goruntu` (`<ad>_goruntu.mp4`; Bayraklar.goruntu veya ayar.goruntu.etkin) → isteğe bağlı `upscale` (`<ad>_upscale.mp4`, UpscaleHatasi yutulur + uyarı). Terimler: `ayar.kok/ayar.stt.terimler_dosyasi` varsa oku, `#` ve boş satırları at, ", " ile birleştir, ≤800 karakterde kes; yoksa "". **Çalışma anı yedeği:** `stt.yaz` `RuntimeError/OSError` fırlatır ve mesajda ("cuda","cublas","cudnn","out of memory","memory") geçiyorsa (küçük harf) günlüğe "CPU'ya düşüldü" yaz, `cihaz.sec(ayar, zorla_cihaz="cpu", zorla_model=bayraklar.model)` ile **bir kez** yeniden dene. Adım hatası: `Sonuc(basarili=False, hata=<Türkçe>)` döner (istisna dışarı sızmaz); `_ara` klasörü hata durumunda KALIR, başarıda `ayar.genel.ara_dosya_sakla` False ise silinir. Video yoksa/okunamazsa (ffprobe `yollar.ffprobe()` ile ilk iş: ses akışı var mı) hata: "ses akışı yok". Günlük: `logging.getLogger("videohat")`.
