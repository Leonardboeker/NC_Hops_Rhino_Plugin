"""
DYNESTIC GH Plugin Icon Generator — Phase 2
Generates 24x24 PNG icons using 4x supersampling for antialiasing.
Style: orange #E8823A background, white geometric shapes, detailed & illustrative.
"""
import math
import os
from PIL import Image, ImageDraw

SCALE = 4
SIZE = 24
S = SIZE * SCALE       # 96x96 canvas
BG   = (232, 130, 58)  # #E8823A orange
W    = (255, 255, 255)  # white
DIM  = (255, 255, 255, 110)  # dim white (semi-transparent)
DARK = (160, 75, 20)   # deep shadow orange
MID  = (200, 105, 40)  # mid-shadow orange

OUT_DIR = os.path.join(os.path.dirname(__file__), '..', 'src', 'DynesticPostProcessor', 'Icons')


def new_canvas():
    img = Image.new('RGBA', (S, S), BG + (255,))
    draw = ImageDraw.Draw(img)
    return img, draw


def finalize(img, name):
    final = img.resize((SIZE, SIZE), Image.LANCZOS)
    final = final.convert('RGB')
    path = os.path.join(OUT_DIR, f'{name}.png')
    final.save(path)
    print(f'  Saved {name}.png')


def p(v):
    """Scale a 0-24 coordinate to canvas space."""
    return int(v * SCALE)


def bezier_pts(p0, p1, p2, n=40):
    """Quadratic bezier points."""
    pts = []
    for i in range(n):
        t = i / (n - 1)
        x = (1-t)**2 * p0[0] + 2*(1-t)*t * p1[0] + t**2 * p2[0]
        y = (1-t)**2 * p0[1] + 2*(1-t)*t * p1[1] + t**2 * p2[1]
        pts.append((int(x), int(y)))
    return pts


def arrowhead(draw, pts, size=16, fill=W):
    """Draw an arrowhead at the end of a polyline."""
    ex, ey = pts[-1]
    ex2, ey2 = pts[-4]
    dx = ex - ex2
    dy = ey - ey2
    length = math.sqrt(dx * dx + dy * dy) or 1
    dx /= length
    dy /= length
    ax1 = ex - int(dx * size) - int(dy * size)
    ay1 = ey - int(dy * size) + int(dx * size)
    ax2 = ex - int(dx * size) + int(dy * size)
    ay2 = ey - int(dy * size) - int(dx * size)
    draw.polygon([(ex, ey), (ax1, ay1), (ax2, ay2)], fill=fill)


def dashed_line(draw, pts, fill=W, width=2, dash=8, gap=5):
    """Draw a dashed line along a list of points."""
    dist = 0
    drawing = True
    toggle = 0
    for i in range(1, len(pts)):
        x0, y0 = pts[i - 1]
        x1, y1 = pts[i]
        seg_len = math.sqrt((x1-x0)**2 + (y1-y0)**2) or 1
        steps = max(1, int(seg_len))
        for s in range(steps):
            fx = x0 + (x1 - x0) * s / steps
            fy = y0 + (y1 - y0) * s / steps
            dist += 1
            if drawing and dist < toggle + dash:
                draw.line([(int(fx), int(fy)), (int(fx)+1, int(fy)+1)], fill=fill, width=width)
            elif dist >= toggle + dash:
                drawing = not drawing
                toggle = dist
                if drawing:
                    toggle += 0
                else:
                    pass


def dashed_pts(draw, pts, fill=W, width=2, dash=10, gap=6):
    """Draw dashed version of a polyline."""
    # Collect all pixel positions along the path
    pixels = []
    for i in range(1, len(pts)):
        x0, y0 = pts[i - 1]
        x1, y1 = pts[i]
        steps = max(int(math.sqrt((x1-x0)**2 + (y1-y0)**2)), 1)
        for s in range(steps):
            t = s / steps
            pixels.append((x0 + (x1-x0)*t, y0 + (y1-y0)*t))

    cycle = dash + gap
    segs = []
    seg = []
    for i, (x, y) in enumerate(pixels):
        pos = i % cycle
        if pos < dash:
            seg.append((int(x), int(y)))
        else:
            if seg:
                segs.append(seg)
            seg = []
    if seg:
        segs.append(seg)

    for seg in segs:
        if len(seg) > 1:
            draw.line(seg, fill=fill, width=width)
        elif seg:
            draw.point(seg, fill=fill)


# ─── HopContour ──────────────────────────────────────────────────────────────
def hop_contour():
    img, draw = new_canvas()
    lw = 5

    # Material edge: thin horizontal line at the bottom (what is being profiled)
    edge_y = p(19)
    draw.line([(p(2), edge_y), (p(22), edge_y)], fill=W, width=2)

    # Bezier arc from material edge up and across — cutting path
    start = (p(3), edge_y)
    ctrl  = (p(4), p(3))
    end   = (p(21.5), p(7))
    pts = bezier_pts(start, ctrl, end, n=50)
    draw.line(pts, fill=W, width=lw)
    arrowhead(draw, pts, size=16)

    # Small filled circle = tool position at start of cut
    sr = p(2.5)
    draw.ellipse([start[0] - sr, start[1] - sr, start[0] + sr, start[1] + sr],
                 fill=DARK, outline=W, width=2)

    # Dashed offset line (kerf on left side of tool)
    off = 14
    pts_off = bezier_pts(
        (start[0] + off, start[1] - off // 2),
        (ctrl[0] + off,  ctrl[1] + off),
        (end[0] - 4,     end[1] + off),
        n=40
    )
    dashed_pts(draw, pts_off, fill=DIM, width=2, dash=8, gap=5)

    finalize(img, 'HopContour')


# ─── HopDrill ────────────────────────────────────────────────────────────────
def hop_drill():
    img, draw = new_canvas()
    lw = 4
    cx, cy = p(12), p(12)
    r_outer = p(8.5)
    r_mid = p(5)
    cone_r = p(3)  # drill cone base radius

    # Dark filled hole interior
    draw.ellipse([cx - r_outer + lw, cy - r_outer + lw,
                  cx + r_outer - lw, cy + r_outer - lw], fill=DARK)
    # Mid depth ring
    draw.ellipse([cx - r_mid, cy - r_mid, cx + r_mid, cy + r_mid],
                 outline=MID, width=2)
    # Outer circle
    draw.ellipse([cx - r_outer, cy - r_outer, cx + r_outer, cy + r_outer],
                 outline=W, width=lw)
    # Crosshair
    ext = p(1.5)
    draw.line([(cx - r_outer - ext, cy), (cx + r_outer + ext, cy)],
              fill=W, width=lw - 1)
    draw.line([(cx, cy - r_outer - ext), (cx, cy + r_outer + ext)],
              fill=W, width=lw - 1)
    # Drill cone tip: filled triangle pointing down (drill bit profile)
    draw.polygon([
        (cx, cy + cone_r),          # tip (downward)
        (cx - cone_r, cy - cone_r), # top-left
        (cx + cone_r, cy - cone_r), # top-right
    ], fill=W)

    finalize(img, 'HopDrill')


# ─── HopRectPocket ───────────────────────────────────────────────────────────
def hop_rect_pocket():
    img, draw = new_canvas()
    lw = 3
    # Three radii (half-width of each concentric square)
    rs = [p(10), p(6.5), p(3.5)]
    cx, cy = S // 2, S // 2

    # Dark center fill
    rc = rs[-1]
    draw.rectangle([cx - rc + lw, cy - rc + lw,
                    cx + rc - lw, cy + rc - lw], fill=DARK)

    # Continuous rectangular spiral path (single polyline, offset pocketing)
    # Trace: outer rect → step corner → middle rect → step corner → inner rect
    pts = []
    step = p(3.5)  # radial step between levels

    # Outer rectangle (clockwise from bottom-left)
    r0 = rs[0]
    pts += [(cx - r0, cy + r0), (cx + r0, cy + r0),
            (cx + r0, cy - r0), (cx - r0, cy - r0),
            (cx - r0, cy - r0 + step)]   # partial left side back down
    # Step inward (top-left diagonal step)
    pts += [(cx - r0 + step, cy - r0 + step)]

    # Middle rectangle (continue clockwise)
    r1 = rs[1]
    pts += [(cx - r1, cy - r1),
            (cx + r1, cy - r1), (cx + r1, cy + r1),
            (cx - r1, cy + r1), (cx - r1, cy - r1 + step)]
    # Step inward again
    pts += [(cx - r1 + step, cy - r1 + step)]

    # Inner rectangle
    r2 = rs[2]
    pts += [(cx - r2, cy - r2),
            (cx + r2, cy - r2), (cx + r2, cy + r2),
            (cx - r2, cy + r2)]

    draw.line(pts, fill=W, width=lw)

    finalize(img, 'HopRectPocket')


# ─── HopCircPocket ───────────────────────────────────────────────────────────
def hop_circ_pocket():
    img, draw = new_canvas()
    lw = 3
    cx, cy = p(12), p(12)
    r_outer = p(9)
    r_inner = p(1.5)

    # True Archimedean spiral (2 full turns, outer → inner)
    n = 300
    pts = []
    for i in range(n):
        t = i / (n - 1)
        angle = t * 4 * math.pi  # 2 full turns
        r = r_outer - (r_outer - r_inner) * t
        x = cx + int(r * math.cos(angle - math.pi / 2))
        y = cy + int(r * math.sin(angle - math.pi / 2))
        pts.append((x, y))
    draw.line(pts, fill=W, width=lw)

    # Small center dot (end of spiral)
    cr = p(1.5)
    draw.ellipse([cx - cr, cy - cr, cx + cr, cy + cr], fill=W)

    finalize(img, 'HopCircPocket')


# ─── HopCircPath ─────────────────────────────────────────────────────────────
def hop_circ_path():
    img, draw = new_canvas()
    lw = 5
    cx, cy = p(12), p(12)
    r = p(8)

    # Main circle (bold ring)
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=W, width=lw)

    # Tool circle ON the path at 45° — filled white with dark border
    angle = math.radians(45)
    tx = cx + int(r * math.cos(angle))
    ty = cy - int(r * math.sin(angle))
    tool_r = p(3.5)
    # Dark border ring around tool
    draw.ellipse([tx - tool_r - 3, ty - tool_r - 3, tx + tool_r + 3, ty + tool_r + 3],
                 fill=DARK)
    draw.ellipse([tx - tool_r, ty - tool_r, tx + tool_r, ty + tool_r], fill=W)

    # Direction arrow: bold arc at top-left with prominent arrowhead
    arc_pts = []
    for deg in range(100, 160, 2):
        rad = math.radians(deg)
        arc_pts.append((cx + int(r * math.cos(rad)), cy - int(r * math.sin(rad))))
    draw.line(arc_pts, fill=W, width=lw + 3)
    arrowhead(draw, arc_pts, size=18)

    finalize(img, 'HopCircPath')


# ─── HopFreeSlot ─────────────────────────────────────────────────────────────
def hop_free_slot():
    img, draw = new_canvas()
    lw = 4

    # Horizontal slot: wide rounded rectangle
    rx, ry = p(2.5), p(7)
    rw, rh = p(19), p(10)
    radius = p(5)

    # Dark interior
    draw.rounded_rectangle([rx + lw, ry + lw, rx + rw - lw, ry + rh - lw],
                            radius=radius - lw, fill=DARK)
    # Slot outline
    draw.rounded_rectangle([rx, ry, rx + rw, ry + rh],
                            radius=radius, outline=W, width=lw)

    # Center axis line with direction arrow
    cy2 = ry + rh // 2
    cx_l = rx + radius
    cx_r = rx + rw - radius
    draw.line([(cx_l, cy2), (cx_r - p(3), cy2)], fill=W, width=3)
    # Arrow tip (pointing right = tool travel direction)
    aw = p(2.5)
    draw.polygon([(cx_r, cy2),
                  (cx_r - int(aw * 2), cy2 - aw),
                  (cx_r - int(aw * 2), cy2 + aw)], fill=W)

    # Tool-radius circles at each end
    for cx2 in [cx_l, cx_r]:
        tr = rh // 2 - lw
        draw.ellipse([cx2 - tr, cy2 - tr, cx2 + tr, cy2 + tr], outline=W, width=2)

    finalize(img, 'HopFreeSlot')


# ─── HopPart ─────────────────────────────────────────────────────────────────
def hop_part():
    img, draw = new_canvas()
    lw = 4

    # L-shaped part (unmistakably "a manufactured part")
    outline = [
        (p(3),  p(3)),
        (p(21), p(3)),
        (p(21), p(13)),
        (p(13), p(13)),
        (p(13), p(21)),
        (p(3),  p(21)),
    ]
    draw.polygon(outline, fill=DARK)
    draw.polygon(outline, outline=W, width=lw)

    # Drill hole: circle with center dot in the top-right lobe
    hx, hy = p(16), p(8)
    hr = p(3)
    draw.ellipse([hx - hr, hy - hr, hx + hr, hy + hr], outline=W, width=2)
    draw.ellipse([hx - p(1.2), hy - p(1.2), hx + p(1.2), hy + p(1.2)], fill=W)

    # Contour indicator on bottom-left lobe: short arc
    draw.arc([p(4), p(14), p(12), p(20)], 180, 270, fill=W, width=3)

    finalize(img, 'HopPart')


# ─── HopSheet ────────────────────────────────────────────────────────────────
def hop_sheet():
    img, draw = new_canvas()
    lw = 5

    # Wide flat rectangle — the sheet face
    x0, y0 = p(2), p(4)
    x1, y1 = S - p(5), S - p(7)   # leave room for 3D edge at right/bottom
    th = p(2.5)  # edge thickness for 3D effect

    # Dark interior
    draw.rectangle([x0 + lw, y0 + lw, x1 - lw, y1 - lw], fill=DARK)
    # Top face outline
    draw.rectangle([x0, y0, x1, y1], outline=W, width=lw)

    # 3D isometric edges (right side + bottom = thickness of sheet)
    edge_col = MID
    # Right edge: parallelogram going down-right
    draw.polygon([
        (x1, y0), (x1 + th, y0 + th),
        (x1 + th, y1 + th), (x1, y1)
    ], fill=edge_col, outline=W, width=2)
    # Bottom edge: parallelogram going right-down
    draw.polygon([
        (x0, y1), (x1, y1),
        (x1 + th, y1 + th), (x0 + th, y1 + th)
    ], fill=edge_col, outline=W, width=2)

    # 2 grain lines on face
    x_l = x0 + lw + p(1)
    x_r = x1 - lw - p(1)
    inner_h = y1 - y0 - 2 * lw
    for frac in [0.35, 0.65]:
        gy = y0 + lw + int(inner_h * frac)
        draw.line([(x_l, gy), (x_r, gy)], fill=W, width=2)

    finalize(img, 'HopSheet')


# ─── HopExport ───────────────────────────────────────────────────────────────
def hop_export():
    img, draw = new_canvas()
    lw = 4

    # Document — left half of icon
    x0, y0 = p(3), p(2.5)
    x1, y1 = p(14), p(21.5)
    fold = p(4)

    doc = [
        (x0,        y0),
        (x1 - fold, y0),
        (x1,        y0 + fold),
        (x1,        y1),
        (x0,        y1),
    ]
    draw.polygon(doc, fill=DARK)
    draw.polygon(doc, outline=W, width=lw)
    # Fold crease
    draw.polygon([(x1 - fold, y0), (x1, y0 + fold), (x1 - fold, y0 + fold)],
                 fill=MID, outline=W, width=2)
    # 3 content lines on document
    for row in [p(9), p(12), p(15)]:
        draw.line([(x0 + p(2), row), (x1 - p(3), row)], fill=W, width=2)

    # Bold export arrow — right portion of icon
    ay = (y0 + y1) // 2
    aw = p(3.5)
    ax0_l = p(13)
    ax1_r = p(22.5)
    draw.line([(ax0_l, ay), (ax1_r - int(aw * 1.5), ay)], fill=W, width=lw + 1)
    draw.polygon([(ax1_r, ay),
                  (ax1_r - int(aw * 2), ay - aw),
                  (ax1_r - int(aw * 2), ay + aw)], fill=W)

    finalize(img, 'HopExport')


# ─── HopSheetExport ──────────────────────────────────────────────────────────
def hop_sheet_export():
    img, draw = new_canvas()
    lw = 3

    # Sheet
    sx0, sy0 = p(1.5), p(3)
    sx1, sy1 = p(19), p(19)
    draw.rectangle([sx0 + lw, sy0 + lw, sx1 - lw, sy1 - lw], fill=DARK)
    draw.rectangle([sx0, sy0, sx1, sy1], outline=W, width=lw)

    # Nested parts — 4 tight rectangles arranged on sheet
    parts = [
        (p(3),  p(5),  p(9),  p(10)),
        (p(10), p(5),  p(17), p(10)),
        (p(3),  p(11), p(9),  p(17)),
        (p(10), p(11), p(17), p(17)),
    ]
    for pr in parts:
        draw.rectangle(pr, fill=MID, outline=W, width=2)

    # Bold export arrow — right side
    ay = (sy0 + sy1) // 2
    aw = p(3)
    ax0_l = p(19)
    ax1_r = p(23.5)
    draw.line([(ax0_l, ay), (ax1_r - int(aw * 1.5), ay)], fill=W, width=lw + 2)
    draw.polygon([(ax1_r, ay),
                  (ax1_r - int(aw * 2), ay - aw),
                  (ax1_r - int(aw * 2), ay + aw)], fill=W)

    finalize(img, 'HopSheetExport')


# ─── HopSaw ──────────────────────────────────────────────────────────────────
def hop_saw():
    img, draw = new_canvas()
    lw = 4

    # Diagonal cut line (miter angle ~45 deg, top-left to bottom-right)
    x0, y0 = p(3),  p(4)
    x1, y1 = p(21), p(20)

    # Kerf band (two parallel lines offset from center line)
    import math
    dx = x1 - x0
    dy = y1 - y0
    length = math.sqrt(dx * dx + dy * dy) or 1
    nx = -dy / length   # perpendicular
    ny =  dx / length
    half = p(1.5)       # half-kerf width in canvas units

    # Filled kerf polygon
    kerf = [
        (int(x0 + nx * half), int(y0 + ny * half)),
        (int(x1 + nx * half), int(y1 + ny * half)),
        (int(x1 - nx * half), int(y1 - ny * half)),
        (int(x0 - nx * half), int(y0 - ny * half)),
    ]
    draw.polygon(kerf, fill=DARK)
    draw.polygon(kerf, outline=W, width=lw)

    # Saw teeth along top edge of kerf (small triangles)
    n_teeth = 6
    for i in range(n_teeth):
        t0 = i / n_teeth
        t1 = (i + 0.5) / n_teeth
        tx0 = x0 + nx * half + dx * t0
        ty0 = y0 + ny * half + dy * t0
        tx1 = x0 + nx * half + dx * t1
        ty1 = y0 + ny * half + dy * t1
        # Tooth tip: offset perpendicular outward
        tip_x = int((tx0 + tx1) / 2 + nx * p(2))
        tip_y = int((ty0 + ty1) / 2 + ny * p(2))
        draw.polygon([
            (int(tx0), int(ty0)),
            (int(tx1), int(ty1)),
            (tip_x,    tip_y),
        ], fill=W)

    # Extend arrows at both ends (shows the extend feature)
    # Arrow at p1 end (backward along cut)
    aw = p(2)
    draw.line([(x0, y0), (x0 - int(dx * 0.15), y0 - int(dy * 0.15))], fill=W, width=lw)
    # Arrow at p2 end (forward along cut)
    draw.line([(x1, y1), (x1 + int(dx * 0.12), y1 + int(dy * 0.12))], fill=W, width=lw)

    finalize(img, 'HopSaw')


def hop_analyzer():
    img, draw = new_canvas()
    lw = 3

    # Document (left side) — represents NC code content
    dx0, dy0 = p(2), p(3)
    dx1, dy1 = p(14), p(21)
    fold = p(3.5)
    doc = [
        (dx0,        dy0),
        (dx1 - fold, dy0),
        (dx1,        dy0 + fold),
        (dx1,        dy1),
        (dx0,        dy1),
    ]
    draw.polygon(doc, fill=DARK)
    draw.polygon(doc, outline=W, width=lw)
    draw.polygon([(dx1 - fold, dy0), (dx1, dy0 + fold), (dx1 - fold, dy0 + fold)],
                 fill=MID, outline=W, width=2)
    # 3 NC-code lines on document
    for row in [p(9), p(13), p(17)]:
        draw.line([(dx0 + p(1.5), row), (dx1 - p(3), row)], fill=W, width=2)

    # Magnifying glass (right side)
    mg_cx, mg_cy = p(17), p(14)
    mg_r = p(5.5)
    # Dark lens interior
    draw.ellipse([mg_cx - mg_r + lw + 1, mg_cy - mg_r + lw + 1,
                  mg_cx + mg_r - lw - 1, mg_cy + mg_r - lw - 1], fill=DARK)
    # Lens ring
    draw.ellipse([mg_cx - mg_r, mg_cy - mg_r,
                  mg_cx + mg_r, mg_cy + mg_r], outline=W, width=lw)
    # Handle — bottom-right diagonal
    h_angle = math.radians(135)
    hx0 = int(mg_cx + (mg_r - p(1)) * math.cos(h_angle))
    hy0 = int(mg_cy + (mg_r - p(1)) * math.sin(h_angle))
    hx1 = int(mg_cx + (mg_r + p(4)) * math.cos(h_angle))
    hy1 = int(mg_cy + (mg_r + p(4)) * math.sin(h_angle))
    draw.line([(hx0, hy0), (hx1, hy1)], fill=W, width=lw + 2)
    # Checkmark inside lens (SP/EP valid = green light)
    ck_x, ck_y = mg_cx - p(1), mg_cy + p(0.5)
    draw.line([(ck_x - p(2), ck_y + p(0.5)),
               (ck_x,        ck_y + p(2.5))], fill=W, width=lw)
    draw.line([(ck_x,        ck_y + p(2.5)),
               (ck_x + p(3), ck_y - p(1.5))], fill=W, width=lw)

    finalize(img, 'HopAnalyzer')


def hop_engraving():
    img, draw = new_canvas()
    lw = 3

    # V-bit tip pointing down at center
    cx, cy = p(12), p(14)
    tip = (cx, p(18))

    # Left side of V (from top-left down to tip)
    draw.line([(p(4), p(6)), tip], fill=W, width=lw)
    # Right side of V (from top-right down to tip)
    draw.line([(p(20), p(6)), tip], fill=W, width=lw)

    # Horizontal top line (surface)
    draw.line([(p(2), p(6)), (p(22), p(6))], fill=DIM, width=lw)

    # Depth tick on left side
    draw.line([(p(3), p(6)), (p(3), p(18))], fill=DIM, width=lw - 1)
    draw.line([(p(2), p(18)), (p(5), p(18))], fill=DIM, width=lw - 1)

    # Wavy engraving path above (suggests text/artwork)
    pts = []
    for i in range(30):
        t = i / 29
        x = p(4 + t * 16)
        y = p(3) + int(math.sin(t * math.pi * 3) * p(1))
        pts.append((x, y))
    for i in range(len(pts) - 1):
        draw.line([pts[i], pts[i+1]], fill=W, width=lw)

    finalize(img, 'HopEngraving')


# ─── Run all ─────────────────────────────────────────────────────────────────
if __name__ == '__main__':
    print('Generating DYNESTIC icons — Phase 2...')
    hop_contour()
    hop_drill()
    hop_rect_pocket()
    hop_circ_pocket()
    hop_circ_path()
    hop_free_slot()
    hop_part()
    hop_sheet()
    hop_export()
    hop_sheet_export()
    hop_saw()
    hop_engraving()
    hop_analyzer()
    print('Done.')
