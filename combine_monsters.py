"""
Script ghép 13 con quái trong CulinariaDungeon thành 1 ảnh tổng hợp đẹp.
Phiên bản cuối - với kích thước frame chính xác.
"""

from PIL import Image, ImageDraw, ImageFont
import os

BASE = r"D:\Project\CulinariaDungeon\Assets\Resouces\Enemy"
BASE2 = r"D:\Project\CulinariaDungeon\Assets\Resouces\2D RPG Platformer Sprites\2D RPG Platformer Sprites\enemies"


def crop_frame(img: Image.Image, col: int, row: int, n_cols: int, n_rows: int) -> Image.Image:
    """Cắt một frame cụ thể từ sprite sheet."""
    w, h = img.size
    fw = w // n_cols
    fh = h // n_rows
    x = col * fw
    y = row * fh
    return img.crop((x, y, x + fw, y + fh))


def trim_transparent(img: Image.Image, margin: int = 2) -> Image.Image:
    """Loại bỏ vùng trong suốt xung quanh ảnh."""
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    bbox = img.getbbox()
    if bbox:
        x0 = max(0, bbox[0] - margin)
        y0 = max(0, bbox[1] - margin)
        x1 = min(img.width, bbox[2] + margin)
        y1 = min(img.height, bbox[3] + margin)
        return img.crop((x0, y0, x1, y1))
    return img


# ─── 13 con quái với thông số frame chính xác ────────────────────────────────
# Bat: 576x64  → 9 cols x 1 row  → frame[0,0]
# DemonSlime: individual png (1 file per frame) → idle_1.png
# Devil (devilkin): 192x224 → 4 cols x 4 rows  → frame[0,0]
# Goblin: 4 cols x 1 row → frame[0,0]
# GruzMother: 1603x3347  → 5 cols x 15 rows → frame[0,2] (row Fly)
# Minotaurus: individual png per frame → idle_1.png
# Mushroom: 560x64 → 5 cols x 1 row (frame w=112) → frame[0,0]
# Plant3 Idle: 256x256 → 4 cols x 4 rows → frame[0,0]
# Skeleton: 264x32 → 4 cols x 1 row → frame[0,0]
# Slime Green Idle: 672x32 → 8 cols x 1 row → frame[0,0]
# Spider: 192x224 → 5 cols x 4 rows → frame[0,0]
# Spider2: 192x224 → 5 cols x 4 rows → frame[0,0]
# Undead: 6 cols x 1 row → frame[0,0]

monsters = [
    {
        "name": "Bat",
        "path": os.path.join(BASE, r"DarkFantasyEnemies_FREE\Bat\Bat with VFX\Bat-IdleFly.png"),
        "col": 0, "row": 0, "n_cols": 9, "n_rows": 1,
        "type": "normal",
    },
    {
        "name": "Demon Slime\n(Boss)",
        "path": os.path.join(BASE, r"boss_demon_slime_FREE_v1.0\boss_demon_slime_FREE_v1.0\individual sprites\01_demon_idle\demon_idle_1.png"),
        "col": 0, "row": 0, "n_cols": 1, "n_rows": 1,
        "type": "boss",
    },
    {
        "name": "Devil",
        "path": os.path.join(BASE2, "devilkin.png"),
        "col": 0, "row": 0, "n_cols": 6, "n_rows": 4,
        "type": "normal",
        "mode": "P",  # ảnh indexed
    },
    {
        "name": "Goblin",
        "path": os.path.join(BASE, r"Hobgoblin\HobgoblinNoOutline\goblinsmasher_idle.png"),
        "col": 0, "row": 0, "n_cols": 4, "n_rows": 1,
        "type": "normal",
    },
    {
        "name": "Gruz Mother\n(Boss)",
        "path": os.path.join(BASE, r"PC _ Computer - Hollow Knight - Bosses - Gruz Mother.png"),
        "col": 0, "row": 2, "n_cols": 5, "n_rows": 15,
        "type": "boss",
    },
    {
        "name": "Minotaurus\n(Boss)",
        "path": os.path.join(BASE, r"mino_v1.1_free\mino_v1.1_free\animations\idle\idle_1.png"),
        "col": 0, "row": 0, "n_cols": 1, "n_rows": 1,
        "type": "boss",
    },
    {
        "name": "Mushroom",
        "path": os.path.join(BASE, r"Forest_Monsters_FREE\Forest_Monsters_FREE\Mushroom\Mushroom with VFX\Mushroom-Idle.png"),
        "col": 0, "row": 0, "n_cols": 5, "n_rows": 1,
        "type": "normal",
    },
    {
        "name": "Predator Plant",
        "path": os.path.join(BASE, r"free-predator-plant-mobs-pixel-art-pack\PNG\Plant3\Idle\Plant3_Idle_full.png"),
        "col": 0, "row": 0, "n_cols": 4, "n_rows": 4,
        "type": "normal",
    },
    {
        "name": "Skeleton",
        "path": os.path.join(BASE, r"Skeleton Sprite Pack\Skeleton\Sprite Sheets\Skeleton Idle.png"),
        "col": 0, "row": 0, "n_cols": 4, "n_rows": 1,
        "type": "normal",
    },
    {
        "name": "Slime",
        "path": os.path.join(BASE, r"Slime Enemy\Slime Enemy\Idle\Sprite Sheet - Green Idle.png"),
        "col": 0, "row": 0, "n_cols": 8, "n_rows": 1,
        "type": "normal",
    },
    {
        "name": "Spider",
        "path": os.path.join(BASE2, "spider.png"),
        "col": 0, "row": 0, "n_cols": 5, "n_rows": 4,
        "type": "normal",
    },
    {
        "name": "Spider 2",
        "path": os.path.join(BASE2, "spider2.png"),
        "col": 0, "row": 0, "n_cols": 5, "n_rows": 4,
        "type": "normal",
    },
    {
        "name": "Undead\nExecutioner",
        "path": os.path.join(BASE, r"Undead executioner\Undead executioner puppet\png\idle.png"),
        "col": 0, "row": 0, "n_cols": 6, "n_rows": 1,
        "type": "boss",
    },
]


def load_monster_image(info: dict) -> Image.Image:
    path = info["path"]
    if not os.path.exists(path):
        print(f"  [WARNING] File not found: {path}")
        return None
    img = Image.open(path)
    # Chuyển sang RGBA
    img = img.convert("RGBA")
    frame = crop_frame(img, info["col"], info["row"], info["n_cols"], info["n_rows"])
    return trim_transparent(frame)


# ─── Layout ──────────────────────────────────────────────────────────────────
COLS = 5
CELL_SIZE = 230
IMG_AREA = 180
PADDING = 14
LABEL_HEIGHT = 46
TITLE_HEIGHT = 96

BG_COLOR        = (11, 9, 26, 255)
CARD_NORMAL     = (26, 24, 54, 255)
CARD_BOSS       = (42, 18, 18, 255)
BORDER_NORMAL   = (85, 65, 195, 255)
BORDER_BOSS     = (200, 75, 45, 255)
LABEL_NORMAL    = (200, 185, 255, 255)
LABEL_BOSS      = (255, 165, 100, 255)
TITLE_COLOR     = (255, 208, 75, 255)
ACCENT_COLOR    = (135, 85, 255, 255)
NUM_COLOR       = (100, 145, 255, 255)

num_monsters = len(monsters)
total_cols = COLS
total_rows = (num_monsters + COLS - 1) // COLS

canvas_w = total_cols * (CELL_SIZE + PADDING) + PADDING
canvas_h = TITLE_HEIGHT + total_rows * (CELL_SIZE + LABEL_HEIGHT + PADDING) + PADDING + 20

canvas = Image.new("RGBA", (canvas_w, canvas_h), BG_COLOR)
draw = ImageDraw.Draw(canvas)

# Gradient nền
for y in range(canvas_h):
    t = y / canvas_h
    r = int(11 + 7 * t)
    g = int(9 + 4 * t)
    b = int(26 + 14 * t)
    draw.line([(0, y), (canvas_w, y)], fill=(r, g, b, 255))

# Load fonts
try:
    font_title = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 32)
    font_sub   = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 14)
    font_label = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 14)
    font_num   = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 12)
    font_badge = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 11)
except Exception:
    font_title = font_sub = font_label = font_num = font_badge = ImageFont.load_default()

# Tiêu đề
title = "CulinariaDungeon — Bestiary (13 Monsters)"
draw.text((canvas_w // 2, 30), title, fill=TITLE_COLOR, font=font_title, anchor="mm")
draw.text((canvas_w // 2, 62), "All enemy types in the game", fill=(155, 140, 200, 220), font=font_sub, anchor="mm")
# Đường trang trí
draw.rectangle([(PADDING, TITLE_HEIGHT - 8), (canvas_w - PADDING, TITLE_HEIGHT - 5)], fill=ACCENT_COLOR)
draw.rectangle([(PADDING, TITLE_HEIGHT - 3), (canvas_w - PADDING, TITLE_HEIGHT - 2)], fill=(55, 45, 115, 255))

# Vẽ từng card
for idx, info in enumerate(monsters):
    col_idx = idx % COLS
    row_idx = idx // COLS
    is_boss = info["type"] == "boss"

    cx = PADDING + col_idx * (CELL_SIZE + PADDING)
    cy = TITLE_HEIGHT + PADDING + row_idx * (CELL_SIZE + LABEL_HEIGHT + PADDING)

    card_rect = [cx, cy, cx + CELL_SIZE, cy + CELL_SIZE + LABEL_HEIGHT]

    # Đổ bóng
    shadow = [cx + 5, cy + 5, cx + CELL_SIZE + 5, cy + CELL_SIZE + LABEL_HEIGHT + 5]
    draw.rectangle(shadow, fill=(0, 0, 0, 70))

    # Nền card
    card_fill = CARD_BOSS if is_boss else CARD_NORMAL
    draw.rectangle(card_rect, fill=card_fill)

    # Vùng ảnh tối hơn một chút
    img_area_rect = [cx + 2, cy + 2, cx + CELL_SIZE - 2, cy + CELL_SIZE - 2]
    overlay = Image.new("RGBA", (CELL_SIZE - 4, CELL_SIZE - 4), (0, 0, 0, 20))
    canvas.paste(overlay, (cx + 2, cy + 2), overlay)

    # Viền ngoài
    border_col = BORDER_BOSS if is_boss else BORDER_NORMAL
    draw.rectangle(card_rect, outline=border_col, width=2)

    # Highlight góc trên trái
    hi_rect = [cx + 1, cy + 1, cx + CELL_SIZE - 1, cy + 18]
    hi_col = (90, 40, 40, 30) if is_boss else (70, 60, 140, 30)
    draw.rectangle(hi_rect, fill=hi_col)

    # Tải ảnh quái
    print(f"Loading [{idx+1:02d}/13]: {info['name'].replace(chr(10), ' ')}")
    monster_img = load_monster_image(info)

    if monster_img:
        img_w, img_h = monster_img.size
        ratio = min(IMG_AREA / img_w, IMG_AREA / img_h)
        new_w = max(1, int(img_w * ratio))
        new_h = max(1, int(img_h * ratio))
        monster_img = monster_img.resize((new_w, new_h), Image.NEAREST)
        paste_x = cx + (CELL_SIZE - new_w) // 2
        paste_y = cy + (CELL_SIZE - new_h) // 2
        canvas.paste(monster_img, (paste_x, paste_y), monster_img)
    else:
        draw.text((cx + CELL_SIZE // 2, cy + CELL_SIZE // 2), "?",
                  fill=(255, 80, 80), font=font_title, anchor="mm")

    # Badge "BOSS"
    if is_boss:
        bw = 42
        bx1 = cx + CELL_SIZE - bw - 4
        by1 = cy + 4
        badge_rect = [bx1, by1, bx1 + bw, by1 + 16]
        draw.rectangle(badge_rect, fill=BORDER_BOSS)
        draw.rectangle(badge_rect, outline=(230, 120, 80, 200), width=1)
        draw.text(((bx1 * 2 + bw) // 2, by1 + 8), "BOSS",
                  fill=(255, 245, 230), font=font_badge, anchor="mm")

    # Số thứ tự
    draw.text((cx + 6, cy + 5), f"#{idx+1}", fill=NUM_COLOR, font=font_num)

    # Đường phân cách ảnh / tên
    sep_y = cy + CELL_SIZE
    draw.rectangle([cx + 4, sep_y - 1, cx + CELL_SIZE - 4, sep_y + 1], fill=border_col)

    # Tên quái
    label_col = LABEL_BOSS if is_boss else LABEL_NORMAL
    draw.text(
        (cx + CELL_SIZE // 2, sep_y + LABEL_HEIGHT // 2),
        info["name"],
        fill=label_col,
        font=font_label,
        anchor="mm",
        align="center",
    )

# Footer
draw.text((canvas_w // 2, canvas_h - 10),
          "CulinariaDungeon  •  Unity 2D Platformer RPG",
          fill=(70, 60, 110, 200), font=font_badge, anchor="mm")

# Lưu
out_path = r"D:\Project\CulinariaDungeon\monsters_collection.png"
canvas.convert("RGB").save(out_path, "PNG")
print(f"\n✅ Đã lưu: {out_path}")
print(f"   Kích thước: {canvas.size[0]} x {canvas.size[1]} px")
