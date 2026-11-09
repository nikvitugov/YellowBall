"""Делает звук «глухим»: срезает высокие частоты (2-полюсный фильтр, Q=1, как AudioLowPassFilter в Unity).
Глухость остаётся в самом файле, поэтому звучит одинаково и в exe, и в браузере (в WebGL фильтры Unity не работают).
Запуск: python Tools/make_muffled.py исходник.wav результат.wav 1600
Так сделан Assets/Scenes/Sounds/jumping.wav (срез 1600 Гц); оригинал — в git до коммита с этим скриптом.
Не прогоняй уже заглушённый файл ещё раз — станет глуше вдвое."""
import sys, wave, math
import numpy as np

src, dst = sys.argv[1], sys.argv[2]
cutoff = float(sys.argv[3]) if len(sys.argv) > 3 else 1600.0
with wave.open(src) as w:
    p = w.getparams()
    x = np.frombuffer(w.readframes(p.nframes), dtype=np.int16).astype(np.float64) / 32768.0
x = x.reshape(-1, p.nchannels)

# RBJ biquad lowpass
w0 = 2 * math.pi * cutoff / p.framerate
alpha = math.sin(w0) / (2 * 1.0)
b0 = b2 = (1 - math.cos(w0)) / 2; b1 = 1 - math.cos(w0)
a0 = 1 + alpha; a1 = -2 * math.cos(w0); a2 = 1 - alpha
b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0
y = np.zeros_like(x)
for c in range(p.nchannels):
    x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x[:, c]):
        o = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, v, y1, o
        y[i, c] = o
y = np.clip(y, -1, 1)
with wave.open(dst, "wb") as w:
    w.setparams(p)
    w.writeframes((y * 32767).astype(np.int16).tobytes())
print("ok", dst, "peak", float(np.abs(y).max()))
