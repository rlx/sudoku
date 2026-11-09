"""Draws the Sudoku icon using only the standard library.

  make_icon.py out.ico          -> Windows icon (several sizes)
  make_icon.py out.png SIZE     -> single PNG
"""
import struct, sys, zlib

def pixel(x, y):
    # x, y in 0..1. White tile, yellow centre square, dark 3x3 grid lines and border.
    border = 0.07
    if x < border or x > 1 - border or y < border or y > 1 - border:
        return (40, 40, 40, 255)
    u = (x - border) / (1 - 2 * border)
    v = (y - border) / (1 - 2 * border)
    for k in (1 / 3, 2 / 3):
        if abs(u - k) < 0.035 or abs(v - k) < 0.035:
            return (40, 40, 40, 255)
    if 1 / 3 < u < 2 / 3 and 1 / 3 < v < 2 / 3:
        return (255, 210, 90, 255)
    if (u < 1 / 3 and v < 1 / 3) or (u > 2 / 3 and v > 2 / 3):
        return (190, 214, 250, 255)
    return (255, 255, 255, 255)

def render(size, ss=4):
    rows = []
    for py in range(size):
        row = bytearray([0])
        for px in range(size):
            acc = [0, 0, 0, 0]
            for sy in range(ss):
                for sx in range(ss):
                    c = pixel((px + (sx + 0.5) / ss) / size, (py + (sy + 0.5) / ss) / size)
                    for i in range(4):
                        acc[i] += c[i]
            row += bytes(a // (ss * ss) for a in acc)
        rows.append(bytes(row))
    raw = b"".join(rows)
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")

if sys.argv[1].endswith(".png"):
    open(sys.argv[1], "wb").write(render(int(sys.argv[2])))
    sys.exit()

sizes = [256, 48, 32, 16]
pngs = [render(s) for s in sizes]
out = struct.pack("<HHH", 0, 1, len(sizes))
offset = 6 + 16 * len(sizes)
for s, p in zip(sizes, pngs):
    out += struct.pack("<BBBBHHII", s % 256, s % 256, 0, 0, 1, 32, len(p), offset)
    offset += len(p)
out += b"".join(pngs)
open(sys.argv[1], "wb").write(out)
