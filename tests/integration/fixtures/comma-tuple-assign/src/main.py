# comma-tuple-assign: x = a, b, c is x = (a, b, c).
#
# adafruit_framebuf writes
#   fill = (color >> 16) & 255, (color >> 8) & 255, color & 255
# without parentheses around the whole RHS. The C# parser stopped at the
# first comma as "Expected newline or end of block".
#
# WHAT DISCRIMINATES: prints 17, 34, 51 (0x11, 0x22, 0x33). A compile that
# truncated fill = (color >> 16) & 255 would not index fill[1]/fill[2].
from pymcu.types import uint32
from pymcu.time import delay_ms

color: uint32 = 0x112233
fill = (color >> 16) & 255, (color >> 8) & 255, color & 255


def main():
    while True:
        print(fill[0])
        print(fill[1])
        print(fill[2])
        print("END")
        delay_ms(1200)
