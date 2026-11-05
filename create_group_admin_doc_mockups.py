from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


OUT_DIR = Path("artifacts")
OUT_DIR.mkdir(exist_ok=True)
DOC_MOCKUPS = [
    OUT_DIR / "group_admin_doc_mockup_1_directory.png",
    OUT_DIR / "group_admin_doc_mockup_2_detail.png",
    OUT_DIR / "group_admin_doc_mockup_3_assign.png",
]


RED = (196, 18, 48)
BLUE = (26, 74, 138)
GOLD = (122, 90, 0)
GREEN = (32, 88, 48)
INK = (26, 26, 26)
MUTED = (102, 102, 102)
BG = (245, 245, 243)
BORDER = (230, 232, 236)
PANEL = (255, 255, 255)
SOFT = (250, 250, 248)


def font(size, bold=False):
    names = ["arialbd.ttf" if bold else "arial.ttf", "segoeuib.ttf" if bold else "segoeui.ttf"]
    for name in names:
        path = f"C:/Windows/Fonts/{name}"
        try:
            return ImageFont.truetype(path, size=size)
        except Exception:
            pass
    return ImageFont.load_default()


def rounded(draw, box, radius, fill, outline=BORDER, width=1):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def button(draw, box, label, fill=RED, fg=(255, 255, 255)):
    rounded(draw, box, 10, fill, None, 0)
    f = font(18, True)
    bbox = draw.textbbox((0, 0), label, font=f)
    x0, y0, x1, y1 = box
    draw.text(((x0 + x1 - bbox[2]) / 2, (y0 + y1 - (bbox[3] - bbox[1])) / 2 - 2), label, font=f, fill=fg)


def chip(draw, box, label, bg, fg):
    rounded(draw, box, 999, bg, None, 0)
    f = font(16, True)
    bbox = draw.textbbox((0, 0), label, font=f)
    x0, y0, x1, y1 = box
    draw.text(((x0 + x1 - bbox[2]) / 2, (y0 + y1 - (bbox[3] - bbox[1])) / 2 - 2), label, font=f, fill=fg)


def input_box(draw, box, placeholder):
    rounded(draw, box, 10, PANEL, (220, 222, 226), 1)
    draw.text((box[0] + 16, box[1] + 13), placeholder, font=font(16), fill=(145, 145, 145))


def header(draw, subtitle, active="Groups"):
    draw.rectangle((0, 0, 1600, 86), fill=PANEL)
    draw.line((0, 86, 1600, 86), fill=(232, 232, 230), width=2)
    draw.rectangle((42, 28, 72, 58), fill=RED)
    draw.text((88, 21), "Admin", font=font(32, True), fill=INK)
    draw.text((90, 55), subtitle, font=font(15), fill=MUTED)
    tabs = ["By quiz", "All questions", "Roles & users", "Groups"]
    x = 64
    for tab in tabs:
        w = draw.textbbox((0, 0), tab, font=font(16, True))[2] + 34
        if tab == active:
            rounded(draw, (x - 14, 112, x + w - 14, 148), 10, (255, 245, 246), RED, 1)
            draw.text((x, 122), tab, font=font(16, True), fill=RED)
        else:
            draw.text((x, 122), tab, font=font(16, True), fill=MUTED)
        x += w + 44


def table(draw, x, y, widths, headers, rows, row_h=68):
    total = sum(widths)
    rounded(draw, (x, y, x + total, y + 54 + row_h * len(rows)), 14, PANEL, BORDER, 1)
    draw.rectangle((x, y, x + total, y + 54), fill=(242, 244, 247))
    cx = x
    for i, h in enumerate(headers):
        draw.text((cx + 18, y + 18), h, font=font(14, True), fill=(120, 120, 120))
        draw.line((cx, y, cx, y + 54 + row_h * len(rows)), fill=BORDER, width=1)
        cx += widths[i]
    draw.line((x + total, y, x + total, y + 54 + row_h * len(rows)), fill=BORDER, width=1)
    draw.line((x, y + 54, x + total, y + 54), fill=BORDER, width=1)
    cy = y + 54
    for row in rows:
        cx = x
        for i, cell in enumerate(row):
            if isinstance(cell, tuple):
                label, bg, fg = cell
                chip(draw, (cx + 18, cy + 18, cx + 140, cy + 44), label, bg, fg)
            else:
                draw.text((cx + 18, cy + 22), cell, font=font(16), fill=INK)
            cx += widths[i]
        draw.line((x, cy + row_h, x + total, cy + row_h), fill=BORDER, width=1)
        cy += row_h


def base():
    im = Image.new("RGB", (1600, 900), BG)
    draw = ImageDraw.Draw(im)
    rounded(draw, (28, 24, 1572, 876), 18, PANEL, (225, 226, 230), 1)
    return im, draw


def directory():
    im, draw = base()
    header(draw, "Group management", "Groups")
    rounded(draw, (56, 190, 356, 822), 16, SOFT, BORDER, 1)
    draw.text((84, 224), "Groups", font=font(24, True), fill=INK)
    draw.text((84, 258), "Reusable access containers", font=font(15), fill=MUTED)
    button(draw, (84, 292, 328, 336), "+ New group")
    groups = [("All groups", "136", True), ("Org - Northeast", "34 orgs", False), ("Org - Enterprise", "56 orgs", False), ("Portfolio - Sales", "5 portfolios", False), ("Portfolio - Operations", "4 portfolios", False)]
    y = 372
    for name, meta, selected in groups:
        rounded(draw, (76, y, 336, y + 58), 12, (255, 245, 246) if selected else PANEL, (243, 205, 212) if selected else BORDER, 1)
        draw.text((98, y + 12), name, font=font(16, True), fill=RED if selected else INK)
        draw.text((98, y + 34), meta, font=font(14), fill=MUTED)
        y += 72

    draw.text((404, 206), "All groups", font=font(30, True), fill=INK)
    draw.text((404, 244), "136 groups total", font=font(16), fill=MUTED)
    button(draw, (1302, 206, 1516, 252), "+ Create group")
    input_box(draw, (404, 284, 1110, 334), "Search group name...")
    chip(draw, (1130, 288, 1260, 330), "All types", (245, 245, 243), INK)
    chip(draw, (1280, 288, 1390, 330), "Active", (233, 244, 238), GREEN)
    rows = [
        ["Org - Northeast", ("Organization", (232, 244, 253), BLUE), "34 orgs", "Org 30110, 30112, 30114...", "Edit"],
        ["Org - Enterprise", ("Organization", (232, 244, 253), BLUE), "56 orgs", "Org 30001-30100", "Edit"],
        ["Portfolio - Sales", ("Portfolio", (255, 244, 232), GOLD), "5 portfolios", "Portfolio 11, 14, 18...", "Edit"],
        ["Portfolio - Operations", ("Portfolio", (255, 244, 232), GOLD), "4 portfolios", "Portfolio 22, 23, 25...", "Edit"],
    ]
    table(draw, 404, 374, [300, 190, 160, 390, 120], ["Group", "Type", "Members", "Scope", ""], rows)
    rounded(draw, (404, 744, 1516, 816), 14, (248, 249, 251), BORDER, 1)
    draw.text((430, 770), "Tip: one group can be reused across multiple users. Changes cascade automatically.", font=font(17), fill=(85, 85, 85))
    im.save(DOC_MOCKUPS[0], quality=95)


def detail():
    im, draw = base()
    header(draw, "Group detail", "Groups")
    draw.text((64, 204), "Org - Northeast", font=font(34, True), fill=INK)
    chip(draw, (64, 252, 206, 288), "Organization", (232, 244, 253), BLUE)
    chip(draw, (224, 252, 334, 288), "Active", (233, 244, 238), GREEN)
    button(draw, (1328, 204, 1516, 252), "Save changes")
    rounded(draw, (64, 328, 746, 490), 16, SOFT, BORDER, 1)
    draw.text((94, 356), "Description", font=font(15, True), fill=(120, 120, 120))
    draw.text((94, 386), "Holds the organizations used for Northeast reporting and access.", font=font(18), fill=INK)
    draw.text((94, 434), "Members", font=font(15, True), fill=(120, 120, 120))
    draw.text((214, 434), "34 organizations", font=font(18), fill=INK)
    draw.text((446, 434), "7 active users", font=font(18), fill=INK)

    draw.text((810, 206), "Included organizations", font=font(26, True), fill=INK)
    input_box(draw, (810, 252, 1290, 302), "Search organizations...")
    button(draw, (1310, 252, 1516, 302), "+ Add")
    rows = [
        ["30110", "Springfield North", ("Included", (233, 244, 238), GREEN)],
        ["30112", "Springfield East", ("Included", (233, 244, 238), GREEN)],
        ["30114", "Cedar Valley", ("Included", (233, 244, 238), GREEN)],
    ]
    table(draw, 810, 334, [150, 420, 260], ["Org ID", "Organization", "Status"], rows)

    draw.text((64, 548), "Preview effective scope", font=font(26, True), fill=INK)
    rounded(draw, (64, 594, 746, 812), 16, (247, 248, 250), BORDER, 1)
    stats = [("34 organizations", 94, 638), ("18 reporting domains", 386, 638), ("92 learners", 94, 724), ("9 modules", 386, 724)]
    for label, x, y in stats:
        rounded(draw, (x, y, x + 230, y + 52), 12, PANEL, BORDER, 1)
        draw.text((x + 22, y + 17), label, font=font(17, True), fill=INK)
    rounded(draw, (810, 650, 1516, 812), 14, (255, 245, 246), (243, 205, 212), 1)
    draw.text((840, 716), "Any change to the group updates every user assigned to it.", font=font(18), fill=(124, 54, 63))
    im.save(DOC_MOCKUPS[1], quality=95)


def assign():
    im, draw = base()
    header(draw, "User access", "Roles & users")
    draw.text((64, 204), "Assign groups to a user", font=font(34, True), fill=INK)
    draw.text((64, 246), "The user inherits organization scope from the selected group(s).", font=font(17), fill=MUTED)
    button(draw, (1328, 204, 1516, 252), "Save access")
    rounded(draw, (64, 304, 746, 456), 16, SOFT, BORDER, 1)
    draw.text((96, 334), "Taylor Morgan", font=font(26, True), fill=INK)
    draw.text((96, 370), "taylor.morgan@example.com", font=font(17), fill=MUTED)
    chip(draw, (96, 402, 190, 438), "Active", (233, 244, 238), GREEN)
    chip(draw, (206, 402, 310, 438), "Learner", (240, 240, 238), MUTED)
    draw.text((810, 306), "Role", font=font(18, True), fill=INK)
    input_box(draw, (810, 338, 1090, 388), "OrgAdmin")
    draw.text((1120, 306), "Search groups", font=font(18, True), fill=INK)
    input_box(draw, (1120, 338, 1516, 388), "Search organization or portfolio groups...")

    draw.text((64, 514), "Selected groups", font=font(26, True), fill=INK)
    rounded(draw, (64, 558, 746, 802), 16, PANEL, BORDER, 1)
    selected = [("Org - Northeast", "Organization group", "34 orgs"), ("Portfolio - Sales", "Portfolio group", "5 portfolios"), ("Portfolio - Operations", "Portfolio group", "4 portfolios")]
    y = 584
    for name, meta, count in selected:
        rounded(draw, (94, y, 716, y + 56), 12, SOFT, BORDER, 1)
        draw.text((118, y + 10), name, font=font(17, True), fill=INK)
        draw.text((118, y + 32), meta, font=font(14), fill=MUTED)
        chip(draw, (586, y + 14, 696, y + 42), count, (240, 240, 238), MUTED)
        y += 70

    draw.text((810, 514), "Effective access preview", font=font(26, True), fill=INK)
    rounded(draw, (810, 558, 1516, 802), 16, (247, 248, 250), BORDER, 1)
    draw.text((842, 590), "The selected groups resolve to:", font=font(16, True), fill=MUTED)
    for label, value, x in [("organizations", "34", 842), ("learners", "92", 1056), ("domains", "18", 1270)]:
        rounded(draw, (x, 636, x + 170, 724), 14, PANEL, BORDER, 1)
        draw.text((x + 26, 656), value, font=font(34, True), fill=RED)
        draw.text((x + 26, 696), label, font=font(15), fill=MUTED)
    draw.text((842, 746), "Access rule", font=font(16, True), fill=MUTED)
    draw.text((842, 772), "OrgAdmin -> groups -> organization IDs -> all reporting filters", font=font(17), fill=INK)
    im.save(DOC_MOCKUPS[2], quality=95)


if __name__ == "__main__":
    directory()
    detail()
    assign()
    for path in DOC_MOCKUPS:
        print(path)
