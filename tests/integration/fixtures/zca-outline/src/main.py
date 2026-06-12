# RFC 0001 Model A (@outline) -- a ZCA method compiled ONCE as a shared subroutine
# that receives the instance's runtime field (self.base) as a parameter, instead of
# being force-inlined per call site. Two Counter instances call the SAME Counter_stepped
# body with different runtime field values:
#
#   a = Counter(65 = 'A'); a.stepped(1) = 65 + 1 = 66 = 'B'
#   b = Counter(97 = 'a'); b.stepped(2) = 97 + 2 = 99 = 'c'
#
# UART output (9600 baud): "OL\n" banner, then 'B', then 'c'  ->  "OLBc".
# Outlining proof: exactly one `Counter_stepped:` label exists in the firmware,
# yet two distinct instances drive it -- no per-instance code duplication.
from pymcu.types import uint8
from pymcu.hal.uart import UART


class Counter:
    def __init__(self, base: uint8):
        self.base = base

    def stepped(self, k: uint8) -> uint8:
        return self.base + k


def main():
    uart = UART(9600)
    uart.println("OL")

    a = Counter(65)
    b = Counter(97)

    uart.write(a.stepped(1))   # 'B'
    uart.write(b.stepped(2))   # 'c'

    while True:
        pass
