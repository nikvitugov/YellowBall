"""
Готовит траву для платформ из Assets/Scenes/Textures/grass.png (трава на чёрном фоне):
  grass_top.png  — полоса травы с прозрачным фоном (травинки над краем + немного земли),
                   бесшовная по горизонтали;
  grass_soil.png — земля из нижней части картинки, бесшовная в обе стороны.
Шейдер YellowBall/GrassPlatform кладёт их на платформы.

Запуск:  python Tools/make_grass.py
"""
import os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, "Assets", "Scenes", "Textures")
SRC = os.path.join(TEX, "grass.png")

OUT_W = 1024          # ширина результата
SEAM = 0.14           # доля ширины на плавную склейку краёв


def seamless_x(img, frac=SEAM):
    """Склеиваем правый край с левым: правый кусок плавно перетекает в левый."""
    h, w = img.shape[:2]
    ov = int(w * frac)
    t = np.linspace(0, 1, ov, dtype=np.float32)[None, :, None]
    out = img[:, :w - ov].copy()
    out[:, :ov] = img[:, w - ov:] * (1 - t) + img[:, :ov] * t
    return out


def main():
    rgb = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32) / 255
    H, W, _ = rgb.shape
    mx = rgb.max(axis=2)

    # Строка, ниже которой фона уже нет (трава/земля сплошные)
    dark_frac = (mx < 0.08).mean(axis=1)
    solid = next(y for y in range(H) if dark_frac[y] < 0.02 and y > H * 0.2)

    # Прозрачность из яркости: чёрный фон -> 0. Только выше сплошной части, чтобы не дырявить землю.
    alpha = np.clip((mx - 0.035) / 0.10, 0, 1)
    ramp = np.clip((np.arange(H) - (solid - 25)) / 25.0, 0, 1)[:, None]   # плавно к 1 у сплошной части
    alpha = np.maximum(alpha, ramp)
    # цвет травинок был смешан с чёрным — возвращаем ему яркость
    col = np.where(alpha[..., None] > 0.02, np.clip(rgb / np.maximum(alpha[..., None], 0.35), 0, 1), 0)

    # Обрезаем пустой верх
    top = max(0, next(y for y in range(H) if (alpha[y] > 0.3).mean() > 0.002) - 4)

    # Где кончается трава и начинается земля (по «зелёности» строк)
    green = (rgb[..., 1] - (rgb[..., 0] + rgb[..., 2]) / 2).mean(axis=1)
    soil_start = next(y for y in range(solid, H) if green[y] < 0.03)

    # Полоса травы: от верха травинок до чуть ниже начала земли; низ плавно уходит в прозрачность,
    # чтобы стыковаться с текстурой земли без шва
    strip_bottom = min(H, soil_start + int((soil_start - top) * 0.12))
    strip_rgb = col[top:strip_bottom]
    strip_a = alpha[top:strip_bottom].copy()
    n = strip_bottom - top
    fade = int(n * 0.12)
    strip_a[n - fade:] *= np.linspace(1, 0, fade)[:, None]
    strip = np.concatenate([strip_rgb, strip_a[..., None]], axis=2)
    strip = seamless_x(strip)

    # Земля: нижняя часть. Выравниваем яркость по строкам (снизу картинка темнее — иначе при повторе
    # получаются полосы) и делаем бесшовной в обе стороны.
    soil = rgb[soil_start + 20:].copy()
    row = soil.mean(axis=(1, 2))
    k = 15
    row_s = np.convolve(np.pad(row, k, mode="edge"), np.ones(2 * k + 1) / (2 * k + 1), mode="valid")
    soil *= (row.mean() / np.maximum(row_s, 1e-3))[:, None, None]
    soil = seamless_x(soil)
    soil = seamless_x(soil.transpose(1, 0, 2), 0.25).transpose(1, 0, 2)

    def save(arr, name, mode):
        img = Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8)).convert(mode)
        h = round(img.height * OUT_W / img.width)
        img.resize((OUT_W, h), Image.LANCZOS).save(os.path.join(TEX, name))
        return OUT_W, h

    sw, sh = save(strip, "grass_top.png", "RGBA")
    ow, oh = save(soil, "grass_soil.png", "RGB")
    print(f"solid={solid} top={top} soil_start={soil_start}")
    print(f"grass_top.png {sw}x{sh} (аспект {sw / sh:.3f}), сама трава занимает верхние "
          f"{(soil_start - top) / (strip_bottom - top):.2f} полосы")
    print(f"grass_soil.png {ow}x{oh}")


if __name__ == "__main__":
    main()
