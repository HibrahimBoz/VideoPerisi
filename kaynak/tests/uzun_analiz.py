"""Uzun kayıt analizi: SRT'yi N dakikalık dilimlere böl; her dilimde terim sayısı + hata oranı.
Kullanım: python uzun_analiz.py cikti.srt referans_paragraf.txt tekrar_suresi_sn [dilim_dk]"""
import re, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from wer import norm, wer
srt, ref, tekrar = sys.argv[1], sys.argv[2], float(sys.argv[3])
dilim = float(sys.argv[4]) if len(sys.argv) > 4 else 10
sys.stdout.reconfigure(encoding='utf-8')
bloklar = re.findall(r'(\d+):(\d+):(\d+),(\d+) --> .*?\n(.*?)(?:\n\n|\Z)', Path(srt).read_text(encoding='utf-8'), re.S)
seg = [(int(h)*3600+int(m)*60+int(s)+int(ms)/1000, t.replace('\n', ' ')) for h, m, s, ms, t in bloklar]
son = seg[-1][0] if seg else 0
terim = ['SIGINT', 'ELINT', 'COMINT', 'RWR', 'emitter', 'TOA', 'AOA', 'burn-through', 'RGPO', 'jammer', 'AESA', 'sidelobe', 'LPI', 'spoofing', 'GPS']
refmetin = Path(ref).read_text(encoding='utf-8')
print(f"segment={len(seg)} son_zaman={son:.0f}s")
for b in range(0, int(son // (dilim * 60)) + 1):
    a0, a1 = b * dilim * 60, (b + 1) * dilim * 60
    metin = ' '.join(t for z, t in seg if a0 <= z < a1)
    if not metin.strip():
        continue
    tekrarlar = max(1, round((min(a1, son) - a0) / tekrar))
    hit = sum(len(re.findall(r'(?<!\w)' + re.escape(x) + r'(?!\w)', metin, re.I)) for x in terim)
    beklenen = tekrarlar * len(terim)
    w = wer(norm(refmetin * tekrarlar), norm(metin)) * 100
    print(f"{a0/60:4.0f}-{a1/60:3.0f} dk: terim {hit}/{beklenen} (%{hit/beklenen*100:.0f})  WER≈%{w:.1f}")
