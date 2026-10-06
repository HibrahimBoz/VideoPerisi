"""Kelime hata oranı (WER). Kullanım: python wer.py beklenen.txt bulunan.txt
Türkçe küçük harf (İ→i, I→ı), noktalama silinir, kelime düzeyinde Levenshtein. jiwer kullanılmaz."""
import re
import sys
from pathlib import Path


def norm(metin: str) -> list[str]:
    metin = metin.replace("İ", "i").replace("I", "ı").lower()
    metin = re.sub(r"[^\w\s]", " ", metin, flags=re.UNICODE)
    return metin.split()


def wer(ref: list[str], hyp: list[str]) -> float:
    n, m = len(ref), len(hyp)
    d = list(range(m + 1))
    for i in range(1, n + 1):
        onceki, d[0] = d[0], i
        for j in range(1, m + 1):
            cur = d[j]
            d[j] = min(d[j] + 1, d[j - 1] + 1, onceki + (ref[i - 1] != hyp[j - 1]))
            onceki = cur
    return d[m] / n if n else 0.0


if __name__ == "__main__":
    r = norm(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
    h = norm(Path(sys.argv[2]).read_text(encoding="utf-8-sig"))
    print(f"WER={wer(r, h) * 100:.1f}% (ref {len(r)} kelime, hyp {len(h)} kelime)")
