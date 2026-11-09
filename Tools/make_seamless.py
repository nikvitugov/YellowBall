"""
Делает бесшовную (для повтора) копию текстуры: выравнивает яркость (убирает виньетку/перепад света
на фото) и плавно склеивает противоположные края.

    python Tools/make_seamless.py Assets/Scenes/Textures/rough-stone-texture-photo.jpg
-> рядом появится rough-stone-texture-photo_seamless.jpg (оригинал не трогается)
"""
import os
import sys
import numpy as np
from PIL import Image, ImageFilter

MAX_SIZE = 2048     # больше Unity всё равно не возьмёт по умолчанию
SEAM = 0.18         # доля стороны на склейку


def flatten_light(img):
    """Делим на сильно размытую копию: уходит крупный перепад яркости, мелкий рельеф остаётся."""
    pil = Image.fromarray((img * 255).astype(np.uint8))
    blur = np.asarray(pil.filter(ImageFilter.GaussianBlur(max(img.shape[:2]) / 6))).astype(np.float32) / 255
    out = img / np.maximum(blur, 1e-3) * blur.mean(axis=(0, 1))
    return np.clip(out, 0, 1)


def seamless_x(img, frac):
    h, w = img.shape[:2]
    ov = int(w * frac)
    t = np.linspace(0, 1, ov, dtype=np.float32)[None, :, None]
    out = img[:, :w - ov].copy()
    out[:, :ov] = img[:, w - ov:] * (1 - t) + img[:, :ov] * t
    return out


def main(path):
    im = Image.open(path).convert("RGB")
    if max(im.size) > MAX_SIZE:
        k = MAX_SIZE / max(im.size)
        im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    img = np.asarray(im).astype(np.float32) / 255
    img = flatten_light(img)
    img = seamless_x(img, SEAM)
    img = seamless_x(img.transpose(1, 0, 2), SEAM).transpose(1, 0, 2)
    root, ext = os.path.splitext(path)
    out = f"{root}_seamless{ext}"
    Image.fromarray((img * 255).astype(np.uint8)).save(out, quality=92)
    print(out, img.shape[1], "x", img.shape[0])


if __name__ == "__main__":
    main(sys.argv[1])
