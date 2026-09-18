# framebuf-slice-assign: buf[i:i+3] = bytes(fill)
#
# adafruit_framebuf RGB888 fill writes
#   fill = (color >> 16) & 255, (color >> 8) & 255, color & 255
#   framebuf.buf[i:i+3] = bytes(fill)
# with a run-time start i and a compile-time length of 3.
#
# WHAT DISCRIMINATES: prints 17, 34, 51 twice (0x11, 0x22, 0x33 of
# 0x112233). Zeros mean the slice copy did not store into buf.
from pymcu.types import uint8, uint32
from pymcu.time import delay_ms

buf = bytearray(6)


def blit(i: uint8, color: uint32):
    fill = (color >> 16) & 255, (color >> 8) & 255, color & 255
    buf[i:i + 3] = bytes(fill)


def main():
    while True:
        blit(0, 0x112233)
        blit(3, 0x112233)
        print(buf[0])
        print(buf[1])
        print(buf[2])
        print(buf[3])
        print(buf[4])
        print(buf[5])
        print("END")
        delay_ms(1200)
