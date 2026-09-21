# Unmodified upstream adafruit_framebuf.py. FrameBuffer.text() exercises the
# CircuitPython idioms PyMCU compiles -- string.split("\n") iteration, enumerate(),
# a BitmapFont allocation guarded on `self._font is None` -- until it reaches
# `open(self.font_name, "rb")` inside BitmapFont.__init__. A PyMCU program has no
# filesystem, so that call is where compilation must stop, naming the resolved
# file and RFC 0008. The test asserts exactly that diagnostic.
import adafruit_framebuf
import pymcu.arena as _pymcu_arena

buf = bytearray(128 * 8)
fb = adafruit_framebuf.FrameBuffer(buf, 128, 8)


def main():
    fb.text("PyMCU", 0, 0, 1)
    while True:
        pass
