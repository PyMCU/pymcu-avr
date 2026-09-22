# PyMCU -- escaping-array: an array retained through an instance field must not
# be overlaid by a sibling call frame.
#
# StackAllocator overlays sibling call subtrees: locals of two calls main()
# makes in sequence share one SRAM region, which is only sound while every
# name in the region dies with its call. self.buf = bytearray(64) creates the
# array inside the inlined __init__ but the object keeps it; the same array
# symbol is then named by use() and verify(). Before the backend promoted such
# arrays to the global section, dev_buf sat inside the region clobber()
# reuses, so clobber()'s stores landed on the buffer's bytes.
#
# clobber()'s frame is deliberately larger than the buffer so the overlap is
# total on the unfixed backend.
#
# Expected UART output:
#   0
#   done
from pymcu.hal.console import print
from pymcu.types import uint8


class Dev:
    def __init__(self):
        self.buf = bytearray(64)

    def fill(self) -> None:
        for i in range(64):
            self.buf[i] = 0xA5

    def check(self) -> uint8:
        bad: uint8 = 0
        for i in range(64):
            if self.buf[i] != 0xA5:
                bad = bad + 1
        return bad


dev = Dev()


def use() -> None:
    dev.fill()


def clobber() -> None:
    scratch: uint8[160] = bytearray(160)
    for i in range(160):
        scratch[i] = 0x00


def verify() -> None:
    print(dev.check())


def main() -> None:
    use()
    clobber()
    verify()
    print("done")


main()
