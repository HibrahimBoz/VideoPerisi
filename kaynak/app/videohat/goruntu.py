"""VideoHat görüntü iyileştirme ve Real-ESRGAN ile büyütme (upscale) modülü."""
import shutil
import subprocess
from pathlib import Path

from . import yollar
from .ayar import Ayar
from .hatalar import IsHatasi, UpscaleHatasi


def _penceresiz() -> int:
    return getattr(subprocess, "CREATE_NO_WINDOW", 0)


def _ffmpeg_calistir(komut: list[str]) -> subprocess.CompletedProcess:
    try:
        return subprocess.run(
            komut,
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            creationflags=_penceresiz(),
        )
    except subprocess.CalledProcessError as hata:
        satirlar = (hata.stderr or "").strip().splitlines()[-5:]
        ayrinti = "\n".join(satirlar)
        mesaj = f"FFmpeg çalıştırma hatası:\n{ayrinti}" if ayrinti else "FFmpeg çalıştırma hatası."
        raise IsHatasi(mesaj) from hata


def _nvidia_smi_calisiyor() -> bool:
    try:
        sonuc = subprocess.run(
            ["nvidia-smi"],
            check=True,
            capture_output=True,
            creationflags=_penceresiz(),
        )
        return sonuc.returncode == 0
    except Exception:
        return False


def _nvenc_destegi_var() -> bool:
    try:
        sonuc = subprocess.run(
            [str(yollar.ffmpeg()), "-encoders"],
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            creationflags=_penceresiz(),
        )
        return "h264_nvenc" in (sonuc.stdout or "")
    except Exception:
        return False


def _fps_al(video: Path) -> str:
    try:
        sonuc = subprocess.run(
            [
                str(yollar.ffprobe()),
                "-v", "error",
                "-select_streams", "v:0",
                "-show_entries", "stream=r_frame_rate",
                "-of", "default=noprint_wrappers=1:nokey=1",
                str(video),
            ],
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            creationflags=_penceresiz(),
        )
        fps = (sonuc.stdout or "").strip()
        if fps and fps != "0/0":
            return fps
    except Exception:
        pass
    return "30"


def iyilestir(
    video: Path,
    temiz_wav: Path,
    cikis: Path,
    ayar: Ayar,
    nvenc: bool,
) -> None:
    """Videoya görüntü filtreleri uygular ve temiz ses ile birleştirir."""
    video = Path(video)
    temiz_wav = Path(temiz_wav)
    cikis = Path(cikis)
    cikis.parent.mkdir(parents=True, exist_ok=True)

    if nvenc and _nvenc_destegi_var() and _nvidia_smi_calisiyor():
        vcodec = ["-c:v", "h264_nvenc", "-cq", str(ayar.goruntu.crf + 1), "-preset", "p5"]
    else:
        vcodec = ["-c:v", "libx264", "-crf", str(ayar.goruntu.crf), "-preset", "medium"]

    komut = [
        str(yollar.ffmpeg()),
        "-y",
        "-i", str(video),
        "-i", str(temiz_wav),
        "-map", "0:v:0",
        "-map", "1:a:0",
        "-vf", "hqdn3d=1.5:1.5:6:6,eq=contrast=1.05:brightness=0.02:saturation=1.05,unsharp=5:5:0.5",
    ] + vcodec + [
        "-c:a", "aac",
        "-b:a", "192k",
        "-shortest",
        "-movflags", "+faststart",
        "-pix_fmt", "yuv420p",
        str(cikis),
    ]
    _ffmpeg_calistir(komut)


def upscale(video: Path, cikis: Path, ayar: Ayar) -> None:
    """Videoyu karelere ayırır, Real-ESRGAN ile büyütür ve ses ile birleştirir."""
    video = Path(video).resolve()
    cikis = Path(cikis).resolve()
    ad = video.stem
    kare_dizini = cikis.parent / "_ara" / f"kareler_{ad}"
    giris_kareler = kare_dizini / "giris"
    cikis_kareler = kare_dizini / "cikis"

    try:
        re_exe = yollar.realesrgan()
    except Exception as e:
        raise UpscaleHatasi(f"Real-ESRGAN aracı bulunamadı: {e}") from e

    cikis.parent.mkdir(parents=True, exist_ok=True)
    giris_kareler.mkdir(parents=True, exist_ok=True)
    cikis_kareler.mkdir(parents=True, exist_ok=True)

    try:
        fps = _fps_al(video)

        # 1. Kareleri PNG olarak çıkar
        try:
            subprocess.run(
                [
                    str(yollar.ffmpeg()),
                    "-y",
                    "-i", str(video),
                    str(giris_kareler / "kare_%08d.png"),
                ],
                check=True,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                creationflags=_penceresiz(),
            )
        except subprocess.CalledProcessError as e:
            ayrinti = "\n".join((e.stderr or "").strip().splitlines()[-5:])
            raise UpscaleHatasi(f"Video kareleri çıkarılamadı:\n{ayrinti}") from e

        giris_pngler = list(giris_kareler.glob("*.png"))
        if not giris_pngler:
            raise UpscaleHatasi("Videodan hiç kare çıkarılamadı.")

        # 2. Real-ESRGAN çalıştır
        komut_re = [
            str(re_exe),
            "-i", str(giris_kareler),
            "-o", str(cikis_kareler),
            "-n", "realesrgan-x4plus",
            "-s", "2",
        ]
        try:
            subprocess.run(
                komut_re,
                check=True,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                cwd=re_exe.parent,
                creationflags=_penceresiz(),
            )
        except subprocess.CalledProcessError as e:
            ayrinti = "\n".join((e.stderr or e.stdout or "").strip().splitlines()[-5:])
            raise UpscaleHatasi(
                f"Real-ESRGAN çalıştırılamadı (Vulkan/GPU hatası olabilir):\n{ayrinti}"
            ) from e
        except Exception as e:
            raise UpscaleHatasi(f"Real-ESRGAN yürütülemedi: {e}") from e

        cikis_pngler = list(cikis_kareler.glob("*.png"))
        if not cikis_pngler:
            raise UpscaleHatasi("Real-ESRGAN büyütülmüş kareler üretmedi.")

        # 3. Kareleri ve sesi yeniden birleştir
        if ayar.goruntu.nvenc and _nvenc_destegi_var() and _nvidia_smi_calisiyor():
            vcodec = ["-c:v", "h264_nvenc", "-cq", str(ayar.goruntu.crf + 1), "-preset", "p5"]
        else:
            vcodec = ["-c:v", "libx264", "-crf", str(ayar.goruntu.crf), "-preset", "medium"]

        komut_birlestir = [
            str(yollar.ffmpeg()),
            "-y",
            "-framerate", fps,
            "-i", str(cikis_kareler / "kare_%08d.png"),
            "-i", str(video),
            "-map", "0:v:0",
            "-map", "1:a:0?",
        ] + vcodec + [
            "-c:a", "copy",
            "-pix_fmt", "yuv420p",
            "-shortest",
            "-movflags", "+faststart",
            str(cikis),
        ]
        try:
            subprocess.run(
                komut_birlestir,
                check=True,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                creationflags=_penceresiz(),
            )
        except subprocess.CalledProcessError as e:
            ayrinti = "\n".join((e.stderr or "").strip().splitlines()[-5:])
            raise UpscaleHatasi(f"Büyütülmüş kareler birleştirilemedi:\n{ayrinti}") from e

    finally:
        if kare_dizini.exists():
            shutil.rmtree(kare_dizini, ignore_errors=True)
