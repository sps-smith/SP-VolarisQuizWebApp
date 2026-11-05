from pathlib import Path

from docx import Document
from docx.enum.section import WD_ORIENT, WD_SECTION
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK, WD_LINE_SPACING
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor


OUT_PATH = Path("artifacts") / "stakeholder_access_groups_brief.docx"
VERTICAL_OUT_PATH = Path("artifacts") / "stakeholder_access_groups_brief_vertical_mockups.docx"
NORMAL_WINDOW_OUT_PATH = Path("artifacts") / "stakeholder_access_groups_brief_normal_window.docx"
MOCKUP_IMAGES = [
    (Path("artifacts") / "group_admin_doc_mockup_1_directory.png", "1. Group directory"),
    (Path("artifacts") / "group_admin_doc_mockup_2_detail.png", "2. Group detail"),
    (Path("artifacts") / "group_admin_doc_mockup_3_assign.png", "3. Assign access"),
]


def configure_portrait(section):
    section.orientation = WD_ORIENT.PORTRAIT
    section.page_width = Inches(8.5)
    section.page_height = Inches(11)
    section.top_margin = Inches(1)
    section.bottom_margin = Inches(1)
    section.left_margin = Inches(1)
    section.right_margin = Inches(1)
    section.header_distance = Inches(0.492)
    section.footer_distance = Inches(0.492)


def configure_landscape(section):
    section.orientation = WD_ORIENT.LANDSCAPE
    section.page_width = Inches(11)
    section.page_height = Inches(8.5)
    section.top_margin = Inches(0.45)
    section.bottom_margin = Inches(0.45)
    section.left_margin = Inches(0.5)
    section.right_margin = Inches(0.5)
    section.header_distance = Inches(0.25)
    section.footer_distance = Inches(0.25)


def set_cell_shading(cell, fill: str) -> None:
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tc_pr.append(shd)
    shd.set(qn("w:fill"), fill)
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")


def set_cell_margins(cell, top=80, start=120, bottom=80, end=120) -> None:
    tc_pr = cell._tc.get_or_add_tcPr()
    tc_mar = tc_pr.first_child_found_in("w:tcMar")
    if tc_mar is None:
        tc_mar = OxmlElement("w:tcMar")
        tc_pr.append(tc_mar)
    for tag, value in (("top", top), ("start", start), ("bottom", bottom), ("end", end)):
        node = tc_mar.find(qn(f"w:{tag}"))
        if node is None:
            node = OxmlElement(f"w:{tag}")
            tc_mar.append(node)
        node.set(qn("w:w"), str(value))
        node.set(qn("w:type"), "dxa")


def set_table_width(table, widths):
    table.autofit = False
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    tbl = table._tbl
    tblPr = tbl.tblPr
    tblW = tblPr.find(qn("w:tblW"))
    if tblW is None:
        tblW = OxmlElement("w:tblW")
        tblPr.append(tblW)
    tblW.set(qn("w:type"), "dxa")
    tblW.set(qn("w:w"), str(sum(widths)))
    tblInd = tblPr.find(qn("w:tblInd"))
    if tblInd is None:
        tblInd = OxmlElement("w:tblInd")
        tblPr.append(tblInd)
    tblInd.set(qn("w:type"), "dxa")
    tblInd.set(qn("w:w"), "120")
    grid = tbl.tblGrid
    for child in list(grid):
        grid.remove(child)
    for width in widths:
        col = OxmlElement("w:gridCol")
        col.set(qn("w:w"), str(width))
        grid.append(col)
    for row in table.rows:
        for idx, cell in enumerate(row.cells):
            tc = cell._tc
            tcPr = tc.get_or_add_tcPr()
            tcW = tcPr.find(qn("w:tcW"))
            if tcW is None:
                tcW = OxmlElement("w:tcW")
                tcPr.append(tcW)
            tcW.set(qn("w:type"), "dxa")
            tcW.set(qn("w:w"), str(widths[idx]))


def set_repeat_header(row):
    trPr = row._tr.get_or_add_trPr()
    tblHeader = OxmlElement("w:tblHeader")
    tblHeader.set(qn("w:val"), "true")
    trPr.append(tblHeader)


def add_paragraph(doc, text="", *, style=None, bold=False, size=11, color="000000",
                  align=WD_ALIGN_PARAGRAPH.LEFT, before=0, after=6, line=1.1,
                  font_name="Calibri", italic=False):
    p = doc.add_paragraph(style=style)
    p.alignment = align
    p.paragraph_format.space_before = Pt(before)
    p.paragraph_format.space_after = Pt(after)
    p.paragraph_format.line_spacing_rule = WD_LINE_SPACING.MULTIPLE
    p.paragraph_format.line_spacing = line
    if text:
        r = p.add_run(text)
        r.bold = bold
        r.italic = italic
        r.font.name = font_name
        r._element.rPr.rFonts.set(qn("w:ascii"), font_name)
        r._element.rPr.rFonts.set(qn("w:hAnsi"), font_name)
        r.font.size = Pt(size)
        r.font.color.rgb = RGBColor.from_string(color)
    return p


def add_bullet(doc, text):
    p = doc.add_paragraph(style="List Bullet")
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.line_spacing_rule = WD_LINE_SPACING.MULTIPLE
    p.paragraph_format.line_spacing = 1.15
    r = p.add_run(text)
    r.font.name = "Calibri"
    r._element.rPr.rFonts.set(qn("w:ascii"), "Calibri")
    r._element.rPr.rFonts.set(qn("w:hAnsi"), "Calibri")
    r.font.size = Pt(11)
    return p


def add_number(doc, text):
    p = doc.add_paragraph(style="List Number")
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.line_spacing_rule = WD_LINE_SPACING.MULTIPLE
    p.paragraph_format.line_spacing = 1.15
    r = p.add_run(text)
    r.font.name = "Calibri"
    r._element.rPr.rFonts.set(qn("w:ascii"), "Calibri")
    r._element.rPr.rFonts.set(qn("w:hAnsi"), "Calibri")
    r.font.size = Pt(11)
    return p


def format_style(style, *, font_name="Calibri", size=11, color="000000", bold=False, before=0, after=6, line=1.1):
    style.font.name = font_name
    style.font.size = Pt(size)
    style.font.bold = bold
    style.font.color.rgb = RGBColor.from_string(color)
    pf = style.paragraph_format
    pf.space_before = Pt(before)
    pf.space_after = Pt(after)
    pf.line_spacing_rule = WD_LINE_SPACING.MULTIPLE
    pf.line_spacing = line
    for attr in ("ascii", "hAnsi", "eastAsia", "cs"):
        style.element.rPr.rFonts.set(qn(f"w:{attr}"), font_name)


def main():
    doc = Document()

    section = doc.sections[0]
    configure_portrait(section)

    normal = doc.styles["Normal"]
    format_style(normal, font_name="Calibri", size=11, color="000000", before=0, after=6, line=1.10)

    for style_name, size, color, before, after in [
        ("Heading 1", 16, "2E74B5", 16, 8),
        ("Heading 2", 13, "2E74B5", 12, 6),
        ("Heading 3", 12, "1F4D78", 8, 4),
    ]:
        st = doc.styles[style_name]
        format_style(st, font_name="Calibri", size=size, color=color, bold=False, before=before, after=after, line=1.10)

    title = doc.add_paragraph()
    title.alignment = WD_ALIGN_PARAGRAPH.LEFT
    title.paragraph_format.space_before = Pt(0)
    title.paragraph_format.space_after = Pt(3)
    title.paragraph_format.line_spacing_rule = WD_LINE_SPACING.MULTIPLE
    title.paragraph_format.line_spacing = 1.0
    r = title.add_run("Organization and Portfolio Access Group Proposal")
    r.font.name = "Calibri"
    r._element.rPr.rFonts.set(qn("w:ascii"), "Calibri")
    r._element.rPr.rFonts.set(qn("w:hAnsi"), "Calibri")
    r.font.size = Pt(20)
    r.font.bold = False
    r.font.color.rgb = RGBColor.from_string("000000")

    subtitle = add_paragraph(
        doc,
        "High-level summary for stakeholder review",
        bold=False,
        size=11,
        color="555555",
        before=0,
        after=12,
        line=1.0,
    )
    subtitle.alignment = WD_ALIGN_PARAGRAPH.LEFT

    add_paragraph(
        doc,
        "Executive Summary",
        style="Heading 1",
        bold=False,
        size=16,
        color="2E74B5",
        before=16,
        after=8,
        line=1.0,
    )
    add_paragraph(
        doc,
        (
            "The current access model assigns users directly to individual organizations or portfolios. "
            "As the number of organizations grows, this creates a high-volume maintenance burden for access updates, "
            "approvals, and audits. The proposed model replaces direct assignment with organization groups and portfolio groups, "
            "so users are granted access through a smaller set of reusable group memberships."
        ),
        before=0,
        after=8,
        line=1.12,
    )

    add_paragraph(
        doc,
        "What Changes",
        style="Heading 1",
        bold=False,
        size=16,
        color="2E74B5",
        before=16,
        after=8,
        line=1.0,
    )

    add_paragraph(
        doc,
        "Proposed Admin Mockups",
        style="Heading 2",
        bold=False,
        size=13,
        color="2E74B5",
        before=12,
        after=4,
        line=1.0,
    )
    caption = add_paragraph(
        doc,
        "Conceptual admin screens showing the group directory, group detail, and user access assignment flow.",
        before=0,
        after=6,
        line=1.05,
    )
    caption.runs[0].font.size = Pt(10.5)
    caption.runs[0].font.color.rgb = RGBColor.from_string("555555")

    for index, (image_path, label) in enumerate(MOCKUP_IMAGES):
        if not image_path.exists():
            continue
        add_paragraph(
            doc,
            label,
            style="Heading 3",
            bold=False,
            size=12,
            color="1F4D78",
            before=8,
            after=4,
            line=1.0,
        )
        image_para = doc.add_paragraph()
        image_para.alignment = WD_ALIGN_PARAGRAPH.CENTER
        image_para.paragraph_format.space_before = Pt(0)
        image_para.paragraph_format.space_after = Pt(10)
        image_para.paragraph_format.line_spacing_rule = WD_LINE_SPACING.SINGLE
        image_para.add_run().add_picture(str(image_path), width=Inches(6.5))
        if index < len(MOCKUP_IMAGES) - 1:
            image_para.runs[-1].add_break(WD_BREAK.PAGE)

    table = doc.add_table(rows=1, cols=2)
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    set_table_width(table, [3880, 5480])
    hdr = table.rows[0]
    hdr.cells[0].text = "Current model"
    hdr.cells[1].text = "Proposed model"
    set_repeat_header(hdr)
    for cell in hdr.cells:
        set_cell_shading(cell, "F2F4F7")
        set_cell_margins(cell)
        cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
        for p in cell.paragraphs:
            p.alignment = WD_ALIGN_PARAGRAPH.LEFT
            p.paragraph_format.space_after = Pt(0)
            for run in p.runs:
                run.font.bold = True
                run.font.size = Pt(10.5)
                run.font.name = "Calibri"
                run._element.rPr.rFonts.set(qn("w:ascii"), "Calibri")
                run._element.rPr.rFonts.set(qn("w:hAnsi"), "Calibri")

    rows = [
        (
            "Users are assigned directly to individual organizations and portfolios.",
            "Users are assigned to organization groups or portfolio groups.",
        ),
        (
            "Every access change can require updating many individual records.",
            "Access changes are managed once at the group level and inherited by members.",
        ),
        (
            "Reporting filters are tied to the user's direct org scope.",
            "Reporting filters continue to use organization IDs, but the IDs are derived from the group scope.",
        ),
    ]
    for left, right in rows:
        row = table.add_row()
        row.cells[0].text = left
        row.cells[1].text = right
        for cell in row.cells:
            set_cell_margins(cell)
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            for p in cell.paragraphs:
                p.paragraph_format.space_after = Pt(0)
                p.paragraph_format.line_spacing_rule = WD_LINE_SPACING.MULTIPLE
                p.paragraph_format.line_spacing = 1.05
                for run in p.runs:
                    run.font.size = Pt(10.5)
                    run.font.name = "Calibri"
                    run._element.rPr.rFonts.set(qn("w:ascii"), "Calibri")
                    run._element.rPr.rFonts.set(qn("w:hAnsi"), "Calibri")

    doc.add_paragraph()

    add_paragraph(
        doc,
        "How It Will Work",
        style="Heading 1",
        bold=False,
        size=16,
        color="2E74B5",
        before=16,
        after=8,
        line=1.0,
    )
    add_bullet(doc, "Organizations and portfolios are first assigned to reusable groups.")
    add_bullet(doc, "Users are assigned to one or more groups rather than to hundreds of individual organizations.")
    add_bullet(doc, "At sign-in or when reporting data is requested, the application resolves the user's group membership into an effective organization scope.")
    add_bullet(doc, "All existing reporting and data filters continue to work against organization IDs, so the downstream query model can remain largely unchanged.")

    add_paragraph(
        doc,
        "Business Benefits",
        style="Heading 1",
        bold=False,
        size=16,
        color="2E74B5",
        before=16,
        after=8,
        line=1.0,
    )
    add_bullet(doc, "Reduces administrative effort by replacing many one-off user assignments with a small number of group assignments.")
    add_bullet(doc, "Improves consistency because the same group can be reused for multiple users and future acquisitions.")
    add_bullet(doc, "Scales better as organization count increases from the current volume to a larger 2026 footprint.")
    add_bullet(doc, "Makes audits and access reviews easier because access can be explained through group membership instead of many direct assignments.")

    add_paragraph(
        doc,
        "Implementation Plan",
        style="Heading 1",
        bold=False,
        size=16,
        color="2E74B5",
        before=16,
        after=8,
        line=1.0,
    )
    add_number(doc, "Define the new data model for organization groups, portfolio groups, and the mapping tables that connect groups to organizations, portfolios, and users.")
    add_number(doc, "Build the scope-resolution service so user group membership expands into the effective organization IDs used by reporting and filtering.")
    add_number(doc, "Update the admin experience so access is assigned through groups and group memberships can be managed centrally.")
    add_number(doc, "Migrate current direct assignments into equivalent groups, validate that users see the same data, and then retire the old direct-assignment workflow.")

    add_paragraph(
        doc,
        "Key Considerations",
        style="Heading 1",
        bold=False,
        size=16,
        color="2E74B5",
        before=16,
        after=8,
        line=1.0,
    )
    add_bullet(doc, "The migration should preserve current access exactly during the transition.")
    add_bullet(doc, "The new model should keep global administrators unchanged.")
    add_bullet(doc, "Role names and user-facing wording may need to be updated so the new group concept is clear.")
    add_bullet(doc, "A small set of unit and integration tests should confirm that group expansion produces the same scoped results as the current model.")

    add_paragraph(
        doc,
        "Recommended Decision",
        style="Heading 1",
        bold=False,
        size=16,
        color="2E74B5",
        before=16,
        after=8,
        line=1.0,
    )
    add_paragraph(
        doc,
        "Proceed with a group-based access model. It is a practical extension of the current architecture and should materially reduce maintenance effort without forcing a redesign of the reporting layer.",
        before=0,
        after=6,
        line=1.12,
    )

    doc.save(NORMAL_WINDOW_OUT_PATH)
    try:
        doc.save(OUT_PATH)
        doc.save(VERTICAL_OUT_PATH)
    except PermissionError:
        pass


if __name__ == "__main__":
    main()
