"""生成 DreamsOutposts 新 UI 的图标贴图。

工作流：所有图标都是静态 PNG 资源（Textures/DreamsOutposts/Ui/*.png），游戏里用
ContentFinder<Texture2D> 引用后按颜色着色（GUI.color 乘算），不再在运行时绘制。

- 坐标空间：20x20（与原型 SVG 的 viewBox 一致），脚本内部按 4 倍超采样绘制再降采样。
- 输出：64x64、RGBA、白色 + alpha（着色时乘 GUI.color），2 的幂 → 游戏会给它生成 mipmap，
  因此可以在 12~34px 之间任意尺寸绘制而不锯齿。
- 描边宽度：1.6 单位（≈5px @64），保证缩到 17px 时线条仍然结实、也不会被 DXT 压缩吃掉。

用法：python tools/gen_ui_icons.py
"""

import math
import os

from PIL import Image, ImageDraw

SPACE = 20.0          # 逻辑坐标空间
OUT_SIZE = 64         # 输出尺寸（2 的幂，带 mipmap）
SUPER = 4             # 超采样倍数
CANVAS = OUT_SIZE * SUPER

OUT_DIR = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Textures", "DreamsOutposts", "Ui",
)

WHITE = (255, 255, 255, 255)


class Canvas:
    """把 20x20 单位坐标画到超大画布上，最后降采样输出。"""

    def __init__(self):
        self.img = Image.new("RGBA", (CANVAS, CANVAS), (255, 255, 255, 0))
        self.draw = ImageDraw.Draw(self.img)

    def s(self, v):
        return v * CANVAS / SPACE

    def pt(self, x, y):
        return (self.s(x), self.s(y))

    def rect(self, x, y, w, h):
        self.draw.rectangle([self.pt(x, y), self.pt(x + w, y + h)], fill=WHITE)

    def round_rect(self, x, y, w, h, r):
        self.draw.rounded_rectangle(
            [self.pt(x, y), self.pt(x + w, y + h)], radius=self.s(r), fill=WHITE
        )

    def round_rect_stroke(self, x, y, w, h, r, thickness):
        self.draw.rounded_rectangle(
            [self.pt(x, y), self.pt(x + w, y + h)],
            radius=self.s(r),
            outline=WHITE,
            width=max(1, int(round(self.s(thickness)))),
        )

    def line(self, x1, y1, x2, y2, thickness):
        width = max(1, int(round(self.s(thickness))))
        self.draw.line([self.pt(x1, y1), self.pt(x2, y2)], fill=WHITE, width=width)
        # PIL 的线没有圆头，两端各补一个圆当圆角端点
        cap = self.s(thickness) / 2.0
        for (cx, cy) in ((x1, y1), (x2, y2)):
            px, py = self.pt(cx, cy)
            self.draw.ellipse([px - cap, py - cap, px + cap, py + cap], fill=WHITE)

    def poly(self, points, thickness, closed):
        pts = list(points)
        if closed:
            pts = pts + [pts[0]]
        for i in range(len(pts) - 1):
            self.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], thickness)

    def fill_poly(self, points):
        self.draw.polygon([self.pt(x, y) for (x, y) in points], fill=WHITE)

    def disc(self, cx, cy, r):
        px, py = self.pt(cx, cy)
        rr = self.s(r)
        self.draw.ellipse([px - rr, py - rr, px + rr, py + rr], fill=WHITE)

    def circle(self, cx, cy, r, thickness):
        px, py = self.pt(cx, cy)
        rr = self.s(r)
        self.draw.ellipse(
            [px - rr, py - rr, px + rr, py + rr],
            outline=WHITE,
            width=max(1, int(round(self.s(thickness)))),
        )

    def arc(self, cx, cy, r, thickness, start_deg, end_deg):
        px, py = self.pt(cx, cy)
        rr = self.s(r)
        # PIL 角度：0° = 3 点钟方向，顺时针增长 —— 与 y 向下的坐标一致，可直接用
        self.draw.arc(
            [px - rr, py - rr, px + rr, py + rr],
            start_deg,
            end_deg,
            fill=WHITE,
            width=max(1, int(round(self.s(thickness)))),
        )

    def save(self, path):
        out = self.img.resize((OUT_SIZE, OUT_SIZE), Image.LANCZOS)
        out.save(path, "PNG")


STROKE = 1.6
GLYPH = 2.0


def draw_grid(c):
    for (x, y) in ((3, 3), (11, 3), (3, 11), (11, 11)):
        c.round_rect_stroke(x, y, 6, 6, 1.5, STROKE)


def draw_shield(c):
    c.poly([(3.5, 5), (16.5, 5), (16.5, 10.5), (10, 17.2), (3.5, 10.5)], STROKE, True)


def draw_crate(c):
    c.poly([(10, 2.8), (17, 6.5), (17, 13.5), (10, 17.2), (3, 13.5), (3, 6.5)], STROKE, True)
    c.poly([(3, 6.5), (10, 10.2), (17, 6.5)], STROKE, False)
    c.line(10, 10.2, 10, 17.2, STROKE)


def draw_bell(c):
    c.arc(10, 9.5, 4.6, STROKE, 180, 360)
    c.line(5.4, 9.5, 5.4, 14, STROKE)
    c.line(14.6, 9.5, 14.6, 14, STROKE)
    c.line(4.4, 14, 15.6, 14, STROKE)
    c.disc(10, 16.3, 1.3)


def draw_factory(c):
    c.rect(4.2, 4.4, 2.2, 4.6)
    c.rect(9.2, 4.4, 2.2, 4.6)
    c.round_rect_stroke(2.8, 9, 14.4, 8, 1.2, STROKE)
    c.rect(8.4, 12.6, 3.2, 4.4)


def draw_mine(c):
    c.poly([(2.6, 16.6), (10, 4.6), (17.4, 16.6)], STROKE, False)
    c.line(2.6, 16.6, 17.4, 16.6, STROKE)
    c.rect(8.2, 11.8, 3.6, 4.8)


def draw_farm(c):
    c.line(10, 17.4, 10, 8.4, STROKE)
    c.poly([(10, 11.6), (6.8, 9.6), (6.2, 6.4), (9.4, 8.2)], STROKE, True)
    c.poly([(10, 11.6), (13.2, 9.6), (13.8, 6.4), (10.6, 8.2)], STROKE, True)


def draw_tree(c):
    c.line(10, 17.4, 10, 11.6, 1.8)
    c.poly([(10, 3), (14.8, 10.6), (5.2, 10.6)], STROKE, True)
    c.poly([(10, 7.4), (16, 15.4), (4, 15.4)], STROKE, True)


def draw_store(c):
    c.poly([(3, 8.6), (10, 4.2), (17, 8.6)], STROKE, False)
    c.round_rect_stroke(4.4, 8.6, 11.2, 8.2, 1.0, STROKE)
    c.rect(8.6, 13.2, 2.8, 3.6)


def draw_turret(c):
    c.line(5, 16.8, 15, 16.8, 1.8)
    c.circle(10, 13, 3.2, STROKE)
    c.line(10, 13, 14.6, 6.2, 2.0)
    c.disc(10, 13, 1.1)


def draw_workshop(c):
    c.circle(10, 10, 4.2, STROKE)
    for i in range(8):
        a = math.radians(i * 45.0)
        c.line(
            10 + math.cos(a) * 4.2, 10 + math.sin(a) * 4.2,
            10 + math.cos(a) * 6.6, 10 + math.sin(a) * 6.6,
            2.0,
        )
    c.disc(10, 10, 1.5)


def draw_refinery(c):
    c.circle(10, 6, 4.6, STROKE)
    c.line(5.4, 6, 5.4, 14.6, STROKE)
    c.line(14.6, 6, 14.6, 14.6, STROKE)
    c.arc(10, 14.6, 4.6, STROKE, 0, 180)


def draw_generator(c):
    c.round_rect_stroke(3.4, 5.2, 13.2, 12.2, 1.4, STROKE)
    c.fill_poly([(11.2, 7.4), (7.4, 11.6), (9.6, 11.6), (8.6, 15.4), (12.6, 11.0), (10.4, 11.0)])


def draw_person(c):
    c.circle(10, 6.8, 3.1, STROKE)
    c.arc(10, 17.6, 6.4, STROKE, 180, 360)


def draw_flag(c):
    c.line(5.4, 3.2, 5.4, 17.4, STROKE)
    c.poly([(5.4, 4.6), (14.6, 7.4), (5.4, 10.2)], STROKE, True)


def draw_plus(c):
    c.line(10, 4.6, 10, 15.4, GLYPH)
    c.line(4.6, 10, 15.4, 10, GLYPH)


def draw_close(c):
    c.line(5.4, 5.4, 14.6, 14.6, GLYPH)
    c.line(14.6, 5.4, 5.4, 14.6, GLYPH)


def draw_check(c):
    c.poly([(4.4, 10.6), (8.4, 14.6), (15.6, 6.2)], GLYPH, False)


def draw_chevron_down(c):
    c.poly([(5.4, 8), (10, 12.6), (14.6, 8)], GLYPH, False)


def draw_menu(c):
    c.disc(10, 5.4, 1.5)
    c.disc(10, 10, 1.5)
    c.disc(10, 14.6, 1.5)


def draw_search(c):
    c.circle(9, 9, 5.2, 1.8)
    c.line(12.9, 12.9, 16.6, 16.6, 2.0)


def draw_info(c):
    c.circle(10, 10, 7.2, STROKE)
    c.disc(10, 6.6, 1.1)
    c.rect(9.2, 9.2, 1.6, 5.2)


def draw_dot(c):
    c.disc(10, 10, 3.0)


# 文件名 = UiIcon 枚举名，C# 侧按名字拼路径
ICONS = {
    "Grid": draw_grid,
    "Shield": draw_shield,
    "Crate": draw_crate,
    "Bell": draw_bell,
    "Factory": draw_factory,
    "Mine": draw_mine,
    "Farm": draw_farm,
    "Tree": draw_tree,
    "Store": draw_store,
    "Turret": draw_turret,
    "Workshop": draw_workshop,
    "Refinery": draw_refinery,
    "Generator": draw_generator,
    "Person": draw_person,
    "Flag": draw_flag,
    "Plus": draw_plus,
    "Close": draw_close,
    "Cross": draw_close,
    "Check": draw_check,
    "ChevronDown": draw_chevron_down,
    "Menu": draw_menu,
    "Search": draw_search,
    "Info": draw_info,
    "Dot": draw_dot,
}


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for name, drawer in ICONS.items():
        canvas = Canvas()
        drawer(canvas)
        canvas.save(os.path.join(OUT_DIR, name + ".png"))
        print("wrote", name + ".png")


if __name__ == "__main__":
    main()
