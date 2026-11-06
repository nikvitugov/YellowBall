"""
Делает объёмную 3D-модель героя из картинки с прозрачным фоном («надувание» силуэта,
как в Monster Mash, только автоматически).

Как работает:
  1. берём силуэт из альфа-канала картинки и чистим его от мусора;
  2. решаем уравнение Пуассона  ∇²h = -2  внутри силуэта (h = 0 на краю);
     высота z = sqrt(2h) — для круга это ровно полусфера, для узкой полоски — круглое сечение;
  3. строим сетку: передняя поверхность (+текстура), задняя — зеркальная копия, края сшиты;
  4. сохраняем OBJ + MTL + текстуру без «ореола» по краям.

Запуск (нужны только numpy и Pillow):
    python Tools/inflate_hero.py
Результат: Assets/Scenes/Textures/hero3d.obj, hero3d.mtl, hero3d_tex.png
"""
import os
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "Scenes", "Textures", "asset01.png")
OUT_DIR = os.path.join(ROOT, "Assets", "Scenes", "Textures")

GRID = 130           # разрешение сетки по большей стороне (больше — детальнее, тяжелее)
DEPTH = 0.8          # 1.0 — круглое сечение, меньше — сплющенный перс
MODEL_HEIGHT = 1.0   # высота модели в юнитах Unity
SMOOTH_EDGE = 6      # сколько раз сглаживать «лесенку» на контуре


def load_mask_and_rgb():
    im = Image.open(SRC).convert("RGBA")
    a = im.getchannel("A")
    # чистка: закрыть мелкие дырки, убрать одиночные точки
    a = a.point(lambda v: 255 if v > 127 else 0)
    a = a.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))   # closing
    a = a.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))   # opening
    mask = np.asarray(a) > 127
    mask = keep_largest_component(mask)
    rgb = np.asarray(im.convert("RGB")).astype(np.float32) / 255.0
    return mask, rgb


def keep_largest_component(mask):
    """Оставить только самый большой связный кусок (без scipy)."""
    h, w = mask.shape
    labels = np.zeros((h, w), np.int32)
    best, best_size, cur = 0, 0, 0
    for y0, x0 in zip(*np.nonzero(mask)):
        if labels[y0, x0]:
            continue
        cur += 1
        stack = [(y0, x0)]
        labels[y0, x0] = cur
        size = 0
        while stack:
            y, x = stack.pop()
            size += 1
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not labels[ny, nx]:
                    labels[ny, nx] = cur
                    stack.append((ny, nx))
        if size > best_size:
            best, best_size = cur, size
    out = labels == best
    # заодно заполнить внутренние дырки: всё, что не достижимо с края снаружи
    outside = np.zeros_like(out)
    stack = [(y, x) for y in (0, h - 1) for x in range(w)] + [(y, x) for x in (0, w - 1) for y in range(h)]
    for y, x in stack:
        outside[y, x] = not out[y, x]
    stack = [(y, x) for y, x in stack if outside[y, x]]
    while stack:
        y, x = stack.pop()
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and not out[ny, nx] and not outside[ny, nx]:
                outside[ny, nx] = True
                stack.append((ny, nx))
    return ~outside


def solve_poisson(inside, iters=3000, tol=1e-6):
    """∇²h = -2 внутри, h = 0 снаружи. Метод сопряжённых градиентов без матриц."""
    def lap(u):
        u = u * inside
        p = np.pad(u, 1)
        r = 4 * u - p[:-2, 1:-1] - p[2:, 1:-1] - p[1:-1, :-2] - p[1:-1, 2:]
        return r * inside

    b = 2.0 * inside.astype(np.float64)
    x = np.zeros_like(b)
    r = b - lap(x)
    p = r.copy()
    rs = (r * r).sum()
    for _ in range(iters):
        Ap = lap(p)
        alpha = rs / (p * Ap).sum()
        x += alpha * p
        r -= alpha * Ap
        rs_new = (r * r).sum()
        if np.sqrt(rs_new) < tol * np.sqrt((b * b).sum()):
            break
        p = r + (rs_new / rs) * p
        rs = rs_new
    return np.maximum(x, 0)


def bleed_texture(rgb, mask, iters=40):
    """Красим прозрачную область цветами края, чтобы на модели не было светлого ореола."""
    valid = np.asarray(Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(7))) > 127
    col = rgb * valid[..., None]
    w = valid.astype(np.float32)
    for _ in range(iters):
        pc = np.pad(col, ((1, 1), (1, 1), (0, 0)))
        pw = np.pad(w, 1)
        s = sum(pc[1 + dy:pc.shape[0] - 1 + dy, 1 + dx:pc.shape[1] - 1 + dx]
                for dy in (-1, 0, 1) for dx in (-1, 0, 1))
        n = sum(pw[1 + dy:pw.shape[0] - 1 + dy, 1 + dx:pw.shape[1] - 1 + dx]
                for dy in (-1, 0, 1) for dx in (-1, 0, 1))
        grow = (~valid) & (n > 0)
        col[grow] = s[grow] / n[grow][:, None]
        w[grow] = 1
        valid = valid | grow
    col[~valid] = rgb[mask].mean(axis=0)
    return col


def remove_eyes(tex, rgb, mask):
    """Текстура для спины: закрашиваем глаза цветом шара вокруг них."""
    H, W = mask.shape
    lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    eyes = np.zeros((H, W), bool)
    y0, y1, x0, x1 = int(0.14 * H), int(0.47 * H), int(0.46 * W), int(0.88 * W)   # где на картинке глаза
    eyes[y0:y1, x0:x1] = lum[y0:y1, x0:x1] < 0.75
    eyes = np.asarray(Image.fromarray((eyes * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(31))) > 127

    # Гладкая заливка от края пятна внутрь (∇²u = 0, на краю — цвета шара).
    ys, xs = np.nonzero(eyes)
    cy0, cy1, cx0, cx1 = ys.min() - 2, ys.max() + 3, xs.min() - 2, xs.max() + 3
    hole = eyes[cy0:cy1, cx0:cx1]
    out = tex.copy()
    for ch in range(3):
        known = tex[cy0:cy1, cx0:cx1, ch] * ~hole
        def nsum(u):
            p = np.pad(u, 1)
            return p[:-2, 1:-1] + p[2:, 1:-1] + p[1:-1, :-2] + p[1:-1, 2:]
        b = nsum(known) * hole
        A = lambda u: (4 * u - nsum(u * hole)) * hole
        x = np.zeros_like(b)
        r = b - A(x); pv = r.copy(); rs = (r * r).sum()
        for _ in range(4000):
            Ap = A(pv); a = rs / (pv * Ap).sum()
            x += a * pv; r -= a * Ap
            rn = (r * r).sum()
            if rn < 1e-10 * hole.sum():
                break
            pv = r + rn / rs * pv; rs = rn
        out[cy0:cy1, cx0:cx1, ch] = np.where(hole, x, tex[cy0:cy1, cx0:cx1, ch])
    # мягкий край пятна, чтобы не было резкой границы
    soft = np.asarray(Image.fromarray((eyes * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(6))).astype(np.float32)[..., None] / 255
    return tex * (1 - soft) + out * soft


def main():
    mask, rgb = load_mask_and_rgb()
    H, W = mask.shape

    # маска на сетке (усреднением, потом порог)
    step = max(H, W) / GRID
    gh, gw = int(round(H / step)), int(round(W / step))
    small = np.asarray(Image.fromarray((mask * 255).astype(np.uint8)).resize((gw, gh), Image.BILINEAR)) / 255.0
    inside = small > 0.5

    h = solve_poisson(inside)
    z = np.sqrt(2.0 * h) * DEPTH            # в клетках сетки

    # Вершины в узлах сетки: узел (i, j) — угол между клетками.
    # Клетка (y, x) входит, если она внутри силуэта. Узел используется, если входит хоть одна его клетка.
    cell = inside
    used = np.zeros((gh + 1, gw + 1), bool)
    full = np.zeros((gh + 1, gw + 1), bool)   # все 4 соседние клетки внутри -> внутренний узел
    pc = np.pad(cell, 1)
    c00, c01, c10, c11 = pc[:-1, :-1], pc[:-1, 1:], pc[1:, :-1], pc[1:, 1:]
    used = c00 | c01 | c10 | c11
    full = c00 & c01 & c10 & c11
    edge = used & ~full

    # высота в узле = среднее высот соседних клеток (на краю 0)
    pz = np.pad(z, 1)
    zn = (pz[:-1, :-1] + pz[:-1, 1:] + pz[1:, :-1] + pz[1:, 1:]) / 4.0
    zn[edge] = 0.0

    ys, xs = np.mgrid[0:gh + 1, 0:gw + 1].astype(np.float64)
    px, py = xs.copy(), ys.copy()

    # сглаживание контура (убираем «лесенку» сетки)
    for _ in range(SMOOTH_EDGE):
        nx, ny = px.copy(), py.copy()
        for (y, x) in zip(*np.nonzero(edge)):
            acc_x, acc_y, k = 0.0, 0.0, 0
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy <= gh and 0 <= xx <= gw and edge[yy, xx]:
                    acc_x += px[yy, xx]; acc_y += py[yy, xx]; k += 1
            if k >= 2:
                nx[y, x] = 0.5 * px[y, x] + 0.5 * acc_x / k
                ny[y, x] = 0.5 * py[y, x] + 0.5 * acc_y / k
        px, py = nx, ny

    # индексы вершин
    front_idx = -np.ones((gh + 1, gw + 1), np.int64)
    back_idx = -np.ones((gh + 1, gw + 1), np.int64)
    verts, uvs = [], []

    # масштаб: высота силуэта -> MODEL_HEIGHT; центр по X, низ на 0
    rows = np.nonzero(used.any(axis=1))[0]
    cols = np.nonzero(used.any(axis=0))[0]
    scale = MODEL_HEIGHT / (rows.max() - rows.min())
    cx = (cols.min() + cols.max()) / 2.0
    bottom = rows.max()

    def to_unity(x, y, zz):
        return ((x - cx) * scale, (bottom - y) * scale, zz * scale)

    for (y, x) in zip(*np.nonzero(used)):
        u, v = px[y, x] / gw, 1.0 - py[y, x] / gh
        front_idx[y, x] = len(verts)
        verts.append(to_unity(px[y, x], py[y, x], -zn[y, x]))   # перед смотрит в -Z (на камеру Unity)
        uvs.append((u, v))
        if edge[y, x]:
            back_idx[y, x] = front_idx[y, x]                     # край общий -> шов без щели
        else:
            back_idx[y, x] = len(verts)
            verts.append(to_unity(px[y, x], py[y, x], +zn[y, x]))
            uvs.append((u, v))

    verts = np.array(verts)
    tris = []
    for (y, x) in zip(*np.nonzero(cell)):
        a, b, c, d = (y, x), (y, x + 1), (y + 1, x + 1), (y + 1, x)
        f = [front_idx[p] for p in (a, b, c, d)]
        k = [back_idx[p] for p in (a, b, c, d)]
        # передняя сторона: обход по часовой, если смотреть с -Z (в системе Unity это «лицо»)
        tris.append((f[0], f[1], f[2])); tris.append((f[0], f[2], f[3]))
        tris.append((k[0], k[2], k[1])); tris.append((k[0], k[3], k[2]))
    tris = np.array(tris)

    # нормали (усреднение по треугольникам, в системе Unity; лицо = по часовой)
    v0, v1, v2 = verts[tris[:, 0]], verts[tris[:, 1]], verts[tris[:, 2]]
    fn = np.cross(v1 - v0, v2 - v0)
    normals = np.zeros_like(verts)
    for i in range(3):
        np.add.at(normals, tris[:, i], fn)
    normals /= np.linalg.norm(normals, axis=1, keepdims=True) + 1e-12

    # Текстура-атлас: левая половина — перед, правая — спина (без глаз).
    # Вершины края дублируем для спины (та же позиция и нормаль, другой UV), щели не будет.
    verts, uvs, normals = list(verts), [(u * 0.5, v) for u, v in uvs], list(normals)
    back_vertices = set(back_idx[back_idx >= 0].tolist()) - set(front_idx[front_idx >= 0].tolist())
    for i in back_vertices:
        uvs[i] = (uvs[i][0] + 0.5, uvs[i][1])
    dup = {}
    for (y, x) in zip(*np.nonzero(edge)):
        i = front_idx[y, x]
        dup[i] = len(verts)
        verts.append(verts[i]); normals.append(normals[i]); uvs.append((uvs[i][0] + 0.5, uvs[i][1]))
    nf = len(tris) // 2
    tris = tris.reshape(-1, 4, 3)                   # на каждую клетку: 2 передних + 2 задних
    back = tris[:, 2:].reshape(-1)
    back = np.array([dup.get(i, i) for i in back.tolist()])
    tris[:, 2:] = back.reshape(-1, 2, 3)
    tris = tris.reshape(-1, 3)
    verts, normals = np.array(verts), np.array(normals)

    front_tex = bleed_texture(rgb, mask)
    back_tex = remove_eyes(front_tex, rgb, mask)
    atlas = np.concatenate([front_tex, back_tex], axis=1)
    Image.fromarray((np.clip(atlas, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT_DIR, "hero3d_tex.png"))

    # OBJ: Unity при импорте меняет знак X и порядок обхода, поэтому пишем X с минусом
    # и треугольники в обратном порядке — после импорта получится ровно то, что выше.
    with open(os.path.join(OUT_DIR, "hero3d.mtl"), "w", encoding="ascii") as m:
        m.write("newmtl hero3d\nKd 1 1 1\nmap_Kd hero3d_tex.png\n")
    with open(os.path.join(OUT_DIR, "hero3d.obj"), "w", encoding="ascii") as o:
        o.write("# Yellow Ball hero, generated by Tools/inflate_hero.py\nmtllib hero3d.mtl\no hero3d\n")
        for vx, vy, vz in verts:
            o.write(f"v {-vx:.5f} {vy:.5f} {vz:.5f}\n")
        for u, v in uvs:
            o.write(f"vt {u:.5f} {v:.5f}\n")
        for nx_, ny_, nz_ in normals:
            o.write(f"vn {-nx_:.5f} {ny_:.5f} {nz_:.5f}\n")
        o.write("usemtl hero3d\ns 1\n")
        for a, b, c in tris + 1:
            o.write(f"f {a}/{a}/{a} {c}/{c}/{c} {b}/{b}/{b}\n")

    print(f"grid {gw}x{gh}, vertices {len(verts)}, triangles {len(tris)}, "
          f"size {np.ptp(verts[:, 0]):.3f} x {np.ptp(verts[:, 1]):.3f} x {np.ptp(verts[:, 2]):.3f}")


if __name__ == "__main__":
    main()
