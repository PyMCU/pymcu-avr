# Runs unchanged under the real MicroPython interpreter, where `framebuf` is
# the C builtin; reference/micropython.txt is what it printed there.
import framebuf

buf = bytearray(64)
fb = framebuf.FrameBuffer(buf, 32, 16, framebuf.MONO_VLSB)
fb.fill(1)
fb.fill(0)
fb.text("Hi!", 0, 0, 1)
fb.hline(0, 8, 32, 1)
fb.vline(31, 0, 16, 1)
fb.rect(1, 9, 10, 6, 1)
fb.rect(14, 9, 5, 5, 1, True)
fb.fill_rect(21, 10, 4, 4, 1)
fb.line(0, 15, 31, 9, 1)
fb.pixel(3, 12, 1)
print(fb.pixel(3, 12))
print(fb.pixel(4, 12))
print(fb.pixel(-1, 0))
print(fb.pixel(0, 16))
i = 0
while i < 64:
    print(buf[i])
    i = i + 1
fb.fill_rect(-5, -5, 10, 10, 1)
fb.fill_rect(28, 12, 10, 10, 1)
fb.line(-10, -10, 40, 30, 1)
fb.scroll(3, 0)
fb.scroll(0, -2)
i = 0
while i < 64:
    print(buf[i])
    i = i + 1
print("END")
