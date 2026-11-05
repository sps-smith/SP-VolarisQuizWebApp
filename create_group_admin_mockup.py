from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


OUT_DIR = Path("artifacts")
OUT_DIR.mkdir(exist_ok=True)
OUT_PATH = OUT_DIR / "group_admin_mockups.png"
VERTICAL_PATH = OUT_DIR / "group_admin_mockups_vertical.png"
SCREEN_PATHS = [
    OUT_DIR / "group_admin_mockup_screen_1.png",
    OUT_DIR / "group_admin_mockup_screen_2.png",
    OUT_DIR / "group_admin_mockup_screen_3.png",
]


def font(size, bold=False):
    candidates = [
        "C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf",
        "C:/Windows/Fonts/segoeui.ttf",
        "C:/Windows/Fonts/segoeuib.ttf",
    ]
    for path in candidates:
        try:
            return ImageFont.truetype(path, size=size)
        except Exception:
            continue
    return ImageFont.load_default()


def rounded(draw, box, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def shadow_card(base, box, radius=24, fill=(255, 255, 255), outline=(227, 229, 232)):
    draw = ImageDraw.Draw(base)
    x0, y0, x1, y1 = box
    for dx, dy, alpha in [(0, 8, 28), (0, 4, 20)]:
        shadow = Image.new("RGBA", base.size, (0, 0, 0, 0))
        sdraw = ImageDraw.Draw(shadow)
        sdraw.rounded_rectangle((x0 + dx, y0 + dy, x1 + dx, y1 + dy), radius=radius, fill=(0, 0, 0, alpha))
        base.alpha_composite(shadow)
    rounded(draw, box, radius, fill, outline)


def text(draw, xy, s, f, fill=(26, 26, 26), anchor="la", spacing=4):
    draw.multiline_text(xy, s, font=f, fill=fill, anchor=anchor, spacing=spacing)


def chip(draw, box, label, fill, fg, f):
    rounded(draw, box, 999, fill, None, 0)
    x0, y0, x1, y1 = box
    bbox = draw.textbbox((0, 0), label, font=f)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    draw.text(((x0 + x1 - tw) / 2, (y0 + y1 - th) / 2 - 1), label, font=f, fill=fg)


def draw_screen_header(draw, box, title, subtitle, active="roles"):
    x0, y0, x1, y1 = box
    top = 56
    rounded(draw, box, 26, (255, 255, 255), (230, 232, 236), 1)
    draw.rectangle((x0, y0, x1, y0 + top), fill=(255, 255, 255))
    draw.rectangle((x0, y0 + top - 1, x1, y0 + top), fill=(232, 232, 230))
    title_f = font(24, bold=True)
    sub_f = font(12)
    nav_f = font(13, bold=True)
    draw.rectangle((x0 + 18, y0 + 16, x0 + 40, y0 + 38), fill=(196, 18, 48))
    draw.text((x0 + 52, y0 + 16), title, font=title_f, fill=(26, 26, 26))
    draw.text((x0 + 52, y0 + 40), subtitle, font=sub_f, fill=(102, 102, 102))
    tabs = [("By quiz", "byquiz"), ("All questions", "allq"), ("Roles & users", "roles"), ("Groups", "groups")]
    tx = x0 + 36
    for label, key in tabs:
        tw = draw.textbbox((0, 0), label, font=nav_f)[2]
        if key == active:
            rounded(draw, (tx - 12, y0 + 68, tx + tw + 12, y0 + 96), 10, (255, 245, 246), (196, 18, 48), 1)
            draw.text((tx, y0 + 74), label, font=nav_f, fill=(196, 18, 48))
        else:
            draw.text((tx, y0 + 74), label, font=nav_f, fill=(102, 102, 102))
        tx += tw + 42


def draw_button(draw, box, label, fill=(196, 18, 48), fg=(255, 255, 255), outline=None):
    rounded(draw, box, 12, fill, outline, 1 if outline else 0)
    f = font(13, bold=True)
    bbox = draw.textbbox((0, 0), label, font=f)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    x0, y0, x1, y1 = box
    draw.text(((x0 + x1 - tw) / 2, (y0 + y1 - th) / 2 - 1), label, font=f, fill=fg)


def draw_tag(draw, box, label, bg, fg):
    chip(draw, box, label, bg, fg, font(12, bold=True))


def draw_table(draw, box, headers, rows, col_widths):
    x0, y0, x1, y1 = box
    header_h = 44
    row_h = 54
    header_bg = (242, 244, 247)
    border = (230, 232, 236)
    f_head = font(11, bold=True)
    f_body = font(12)
    rounded(draw, box, 18, (255, 255, 255), border, 1)
    draw.rectangle((x0, y0, x1, y0 + header_h), fill=header_bg)
    cx = x0
    for i, h in enumerate(headers):
        draw.line((cx, y0, cx, y1), fill=border, width=1)
        draw.text((cx + 14, y0 + 14), h, font=f_head, fill=(120, 120, 120))
        cx += col_widths[i]
    draw.line((x1 - 1, y0, x1 - 1, y1), fill=border, width=1)
    draw.line((x0, y0 + header_h, x1, y0 + header_h), fill=border, width=1)
    cy = y0 + header_h
    for r_idx, row in enumerate(rows):
        if cy + row_h > y1:
            break
        draw.rectangle((x0, cy, x1, cy + row_h), fill=(255, 255, 255))
        cx = x0
        for c_idx, cell in enumerate(row):
            draw.line((cx, cy, cx, cy + row_h), fill=border, width=1)
            if isinstance(cell, tuple) and cell and cell[0] == "tag":
                label, bg, fg = cell[1], cell[2], cell[3]
                chip_w = min(col_widths[c_idx] - 24, max(56, draw.textbbox((0, 0), label, font=font(12, bold=True))[2] + 18))
                chip(draw, (cx + 14, cy + 12, cx + 14 + chip_w, cy + 32), label, bg, fg, font(12, bold=True))
            else:
                draw.text((cx + 14, cy + 16), str(cell), font=f_body, fill=(26, 26, 26))
            cx += col_widths[c_idx]
        draw.line((x0, cy + row_h, x1, cy + row_h), fill=border, width=1)
        cy += row_h


def draw_left_panel(screen, x0, y0, w, h):
    draw = ImageDraw.Draw(screen)
    rounded(draw, (x0, y0, x0 + w, y0 + h), 22, (250, 250, 248), (232, 232, 230), 1)
    f1 = font(12, bold=True)
    f2 = font(11)
    draw.text((x0 + 18, y0 + 18), "Groups", font=font(15, bold=True), fill=(26, 26, 26))
    draw.text((x0 + 18, y0 + 42), "Reusable access containers", font=f2, fill=(102, 102, 102))
    draw_button(draw, (x0 + 18, y0 + 64, x0 + w - 18, y0 + 98), "+ New group", fill=(196, 18, 48))
    items = [
        ("All groups", "128", True),
        ("Org - Northeast", "34 orgs", False),
        ("Org - Enterprise", "56 orgs", False),
        ("Portfolio - Sales", "5 portfolios", False),
        ("Portfolio - Operations", "4 portfolios", False),
    ]
    iy = y0 + 122
    for name, meta, sel in items:
        if sel:
            rounded(draw, (x0 + 12, iy, x0 + w - 12, iy + 52), 14, (255, 245, 246), (243, 205, 212), 1)
            name_fill = (196, 18, 48)
        else:
            rounded(draw, (x0 + 12, iy, x0 + w - 12, iy + 52), 14, (255, 255, 255), (234, 234, 230), 1)
            name_fill = (26, 26, 26)
        draw.text((x0 + 28, iy + 12), name, font=f1, fill=name_fill)
        draw.text((x0 + 28, iy + 30), meta, font=f2, fill=(102, 102, 102))
        iy += 62


def draw_group_overview():
    canvas = Image.new("RGBA", (3200, 1650), (245, 245, 243, 255))
    draw = ImageDraw.Draw(canvas)
    title_f = font(30, bold=True)
    sub_f = font(16)
    draw.text((96, 72), "Admin screen mockups for group-based access", font=title_f, fill=(26, 26, 26))
    draw.text((96, 114), "Three screens showing the proposed workflow for managing organization groups and portfolio groups.", font=sub_f, fill=(102, 102, 102))

    positions = [(90, 180, 990, 1380), (1150, 180, 2050, 1380), (2210, 180, 3110, 1380)]
    labels = ["1. Group directory", "2. Group detail", "3. Assign access"]
    captions = [
        "Browse groups, create new ones, and see membership counts at a glance.",
        "Edit one group, manage included orgs/portfolios, and review its scope.",
        "Assign a user to groups and preview the effective access before saving.",
    ]
    for idx, (box, label, cap) in enumerate(zip(positions, labels, captions), start=1):
        x0, y0, x1, y1 = box
        shadow_card(canvas, box)
        rounded(draw, (x0 + 1, y0 + 1, x1 - 1, y1 - 1), 24, (255, 255, 255), None, 0)
        header = font(16, bold=True)
        draw.text((x0, y0 - 36), label, font=header, fill=(26, 26, 26))
        draw.text((x0, y0 - 12), cap, font=font(12), fill=(102, 102, 102))

    # Screen 1
    x0, y0, x1, y1 = positions[0]
    draw_screen_header(draw, (x0, y0, x1, y1), "Admin", "Group management", active="groups")
    draw_left_panel(canvas, x0 + 16, y0 + 116, 190, 1040)
    main_x = x0 + 222
    draw_button(draw, (main_x + 320, y0 + 122, main_x + 500, y0 + 156), "+ Create group")
    draw.text((main_x, y0 + 126), "All groups", font=font(18, bold=True), fill=(26, 26, 26))
    draw.text((main_x, y0 + 150), "136 groups total", font=font(12), fill=(102, 102, 102))
    # search and filters
    rounded(draw, (main_x, y0 + 190, main_x + 410, y0 + 224), 10, (255, 255, 255), (222, 224, 228), 1)
    draw.text((main_x + 12, y0 + 198), "Search group name...", font=font(12), fill=(150, 150, 150))
    chip(draw, (main_x + 430, y0 + 190, main_x + 545, y0 + 224), "All types", (245, 245, 243), (26, 26, 26), font(12, bold=True))
    chip(draw, (main_x + 556, y0 + 190, main_x + 670, y0 + 224), "Active", (233, 244, 238), (32, 88, 48), font(12, bold=True))
    headers = ["Group", "Type", "Members", "Scope", ""]
    rows = [
        ["Org - Northeast", ("tag", "Organization", (232, 244, 253), (26, 74, 138)), "34 orgs", "Org 30110, 30112...", "Edit"],
        ["Org - Enterprise", ("tag", "Organization", (232, 244, 253), (26, 74, 138)), "56 orgs", "Org 30001-30100", "Edit"],
        ["Portfolio - Sales", ("tag", "Portfolio", (255, 244, 232), (122, 90, 0)), "5 portfolios", "Portfolio 11, 14...", "Edit"],
        ["Portfolio - Operations", ("tag", "Portfolio", (255, 244, 232), (122, 90, 0)), "4 portfolios", "Portfolio 22, 23...", "Edit"],
    ]
    draw_table(draw, (main_x, y0 + 248, x1 - 22, y1 - 40), headers, rows, [180, 110, 100, 200, 66])
    # footer note
    rounded(draw, (main_x, y1 - 112, x1 - 22, y1 - 52), 14, (248, 249, 251), (232, 232, 230), 1)
    draw.text((main_x + 16, y1 - 94), "Tip: one group can be reused across multiple users. Changes cascade automatically.", font=font(12), fill=(85, 85, 85))

    # Screen 2
    x0, y0, x1, y1 = positions[1]
    draw_screen_header(draw, (x0, y0, x1, y1), "Admin", "Group detail", active="groups")
    draw.text((x0 + 26, y0 + 120), "Org - Northeast", font=font(22, bold=True), fill=(26, 26, 26))
    chip(draw, (x0 + 26, y0 + 156, x0 + 126, y0 + 186), "Organization", (232, 244, 253), (26, 74, 138), font(12, bold=True))
    chip(draw, (x0 + 136, y0 + 156, x0 + 244, y0 + 186), "Active", (233, 244, 238), (32, 88, 48), font(12, bold=True))
    draw_button(draw, (x0 + 520, y0 + 122, x1 - 26, y0 + 156), "Save changes")
    draw.text((x0 + 26, y0 + 214), "Overview", font=font(14, bold=True), fill=(26, 26, 26))
    rounded(draw, (x0 + 26, y0 + 246, x1 - 26, y0 + 374), 18, (250, 250, 248), (232, 232, 230), 1)
    draw.text((x0 + 44, y0 + 268), "Description", font=font(11, bold=True), fill=(120, 120, 120))
    draw.text((x0 + 44, y0 + 292), "Holds the organizations used for Northeast reporting and access.", font=font(13), fill=(26, 26, 26))
    draw.text((x0 + 44, y0 + 324), "Members", font=font(11, bold=True), fill=(120, 120, 120))
    draw.text((x0 + 150, y0 + 324), "34 organizations", font=font(13), fill=(26, 26, 26))
    draw.text((x0 + 360, y0 + 324), "7 active users", font=font(13), fill=(26, 26, 26))
    draw.text((x0 + 26, y0 + 402), "Included organizations", font=font(16, bold=True), fill=(26, 26, 26))
    rounded(draw, (x0 + 26, y0 + 438, x1 - 26, y0 + 692), 18, (255, 255, 255), (232, 232, 230), 1)
    search_w = x1 - x0 - 150
    rounded(draw, (x0 + 42, y0 + 454, x0 + 42 + search_w, y0 + 488), 10, (255, 255, 255), (222, 224, 228), 1)
    draw.text((x0 + 54, y0 + 462), "Search organizations...", font=font(12), fill=(150, 150, 150))
    draw_button(draw, (x1 - 140, y0 + 454, x1 - 42, y0 + 488), "+ Add")
    table_box = (x0 + 42, y0 + 506, x1 - 42, y0 + 662)
    draw_table(draw, table_box, ["Org ID", "Organization", "Status"], [
        ["30110", "Springfield North", ("tag", "Included", (233, 244, 238), (32, 88, 48))],
        ["30112", "Springfield East", ("tag", "Included", (233, 244, 238), (32, 88, 48))],
        ["30114", "Cedar Valley", ("tag", "Included", (233, 244, 238), (32, 88, 48))],
    ], [130, 420, 266])
    draw.text((x0 + 26, y0 + 720), "Preview effective scope", font=font(16, bold=True), fill=(26, 26, 26))
    rounded(draw, (x0 + 26, y0 + 756, x1 - 26, y0 + 1070), 18, (247, 248, 250), (232, 232, 230), 1)
    draw.text((x0 + 46, y0 + 782), "Users in this group can see:", font=font(12, bold=True), fill=(102, 102, 102))
    for i, t in enumerate(["34 organizations", "18 reporting domains", "92 learners", "9 modules"], start=0):
        rounded(draw, (x0 + 46 + (i % 2) * 230, y0 + 820 + (i // 2) * 92, x0 + 220 + (i % 2) * 230, y0 + 878 + (i // 2) * 92), 16, (255, 255, 255), (232, 232, 230), 1)
        draw.text((x0 + 66 + (i % 2) * 230, y0 + 836 + (i // 2) * 92), t, font=font(13, bold=True), fill=(26, 26, 26))
    # note
    rounded(draw, (x0 + 26, y1 - 112, x1 - 26, y1 - 52), 14, (255, 245, 246), (243, 205, 212), 1)
    draw.text((x0 + 42, y1 - 94), "Any change to the group updates every user assigned to it.", font=font(12), fill=(124, 54, 63))

    # Screen 3
    x0, y0, x1, y1 = positions[2]
    draw_screen_header(draw, (x0, y0, x1, y1), "Admin", "User access", active="roles")
    draw.text((x0 + 24, y0 + 120), "Assign groups to a user", font=font(22, bold=True), fill=(26, 26, 26))
    draw.text((x0 + 24, y0 + 152), "The user inherits organization scope from the selected group(s).", font=font(12), fill=(102, 102, 102))
    draw_button(draw, (x0 + 470, y0 + 120, x1 - 24, y0 + 154), "Save access")
    # user card
    rounded(draw, (x0 + 24, y0 + 194, x1 - 24, y0 + 314), 18, (250, 250, 248), (232, 232, 230), 1)
    draw.text((x0 + 44, y0 + 216), "Taylor Morgan", font=font(18, bold=True), fill=(26, 26, 26))
    draw.text((x0 + 44, y0 + 245), "taylor.morgan@example.com", font=font(12), fill=(102, 102, 102))
    chip(draw, (x0 + 44, y0 + 272, x0 + 120, y0 + 300), "Active", (233, 244, 238), (32, 88, 48), font(12, bold=True))
    chip(draw, (x0 + 130, y0 + 272, x0 + 232, y0 + 300), "Learner", (240, 240, 238), (102, 102, 102), font(12, bold=True))
    draw.text((x0 + 24, y0 + 340), "Role", font=font(13, bold=True), fill=(26, 26, 26))
    rounded(draw, (x0 + 24, y0 + 370, x0 + 246, y0 + 406), 10, (255, 255, 255), (222, 224, 228), 1)
    draw.text((x0 + 38, y0 + 379), "OrgAdmin", font=font(12), fill=(26, 26, 26))
    draw.text((x0 + 212, y0 + 379), "v", font=font(12), fill=(150, 150, 150))
    draw.text((x0 + 276, y0 + 340), "Search groups", font=font(13, bold=True), fill=(26, 26, 26))
    rounded(draw, (x0 + 276, y0 + 370, x1 - 24, y0 + 406), 10, (255, 255, 255), (222, 224, 228), 1)
    draw.text((x0 + 290, y0 + 379), "Search organization or portfolio groups...", font=font(12), fill=(150, 150, 150))
    draw.text((x0 + 24, y0 + 438), "Selected groups", font=font(16, bold=True), fill=(26, 26, 26))
    rounded(draw, (x0 + 24, y0 + 474, x1 - 24, y0 + 708), 18, (255, 255, 255), (232, 232, 230), 1)
    group_rows = [
        ("Org - Northeast", "Organization", "34 orgs"),
        ("Portfolio - Sales", "Portfolio", "5 portfolios"),
        ("Portfolio - Operations", "Portfolio", "4 portfolios"),
    ]
    gy = y0 + 494
    for name, gtype, meta in group_rows:
        rounded(draw, (x0 + 40, gy, x1 - 40, gy + 54), 14, (250, 250, 248), (235, 236, 238), 1)
        draw.text((x0 + 56, gy + 12), name, font=font(13, bold=True), fill=(26, 26, 26))
        draw.text((x0 + 56, gy + 31), f"{gtype} group", font=font(11), fill=(102, 102, 102))
        chip(draw, (x1 - 150, gy + 12, x1 - 58, gy + 38), meta, (240, 240, 238), (102, 102, 102), font(11, bold=True))
        gy += 66
    draw.text((x0 + 24, y0 + 744), "Effective access preview", font=font(16, bold=True), fill=(26, 26, 26))
    rounded(draw, (x0 + 24, y0 + 780, x1 - 24, y0 + 1068), 18, (247, 248, 250), (232, 232, 230), 1)
    text(draw, (x0 + 44, y0 + 804), "The selected groups resolve to:", font(12, bold=True), fill=(102, 102, 102))
    stats = [("34", "organizations"), ("92", "learners"), ("18", "domains")]
    sx = x0 + 50
    for n, label in stats:
        rounded(draw, (sx, y0 + 842, sx + 146, y0 + 928), 18, (255, 255, 255), (232, 232, 230), 1)
        draw.text((sx + 18, y0 + 862), n, font=font(24, bold=True), fill=(196, 18, 48))
        draw.text((sx + 18, y0 + 894), label, font=font(12), fill=(102, 102, 102))
        sx += 166
    draw.text((x0 + 44, y0 + 952), "Access rule", font=font(12, bold=True), fill=(102, 102, 102))
    draw.text((x0 + 44, y0 + 974), "OrgAdmin -> groups -> organization IDs -> all reporting filters", font=font(13), fill=(26, 26, 26))
    rounded(draw, (x0 + 24, y1 - 112, x1 - 24, y1 - 52), 14, (250, 250, 248), (232, 232, 230), 1)
    draw.text((x0 + 42, y1 - 94), "This keeps the current app logic intact while removing direct org assignment noise.", font=font(12), fill=(85, 85, 85))

    rgb = canvas.convert("RGB")

    crops = [rgb.crop(box) for box in positions]
    for path, crop in zip(SCREEN_PATHS, crops):
        crop.save(path, quality=95)
    padding = 72
    label_space = 68
    widths = [c.width for c in crops]
    heights = [c.height for c in crops]
    stacked_w = max(widths)
    stacked_h = sum(heights) + padding * 2 + label_space * 3
    stacked = Image.new("RGB", (stacked_w + 120, stacked_h), (245, 245, 243))
    sdraw = ImageDraw.Draw(stacked)
    title_f = font(18, bold=True)
    body_f = font(12)
    y = padding
    labels = ["1. Group directory", "2. Group detail", "3. Assign access"]
    captions = [
        "Browse groups, create new ones, and see membership counts at a glance.",
        "Edit one group, manage included orgs/portfolios, and review its scope.",
        "Assign a user to groups and preview the effective access before saving.",
    ]
    for idx, (crop, label, cap) in enumerate(zip(crops, labels, captions)):
        sdraw.text((60, y - 36), label, font=title_f, fill=(26, 26, 26))
        sdraw.text((60, y - 14), cap, font=body_f, fill=(102, 102, 102))
        stacked.paste(crop, (60, y))
        y += crop.height + padding + label_space

    stacked.save(VERTICAL_PATH, quality=95)
    stacked.save(OUT_PATH, quality=95)
    print(OUT_PATH)


if __name__ == "__main__":
    draw_group_overview()
