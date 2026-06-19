# PyMCU -- divmod16: the divmod() built-in at 16-bit width.
#
# Verifies that divmod() produces 16-bit results. The earlier implementation
# hard-coded uint8 result variables (and __div8/__mod8), so a wide quotient was
# silently narrowed: 3000 // 10 = 300 became 300 & 0xFF == 44.
#
# UART output (9600 baud, print() appends its own newline):
#   "DM\n"   -- boot banner
#   "300\n"  -- 3000 // 10 = 300  (> 255: proves the quotient keeps 16 bits)
#   "0\n"    -- 3000 % 10  = 0
from pymcu.types import uint16
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("DM")

    q: uint16 = 0
    r: uint16 = 0
    q, r = divmod(3000, 10)
    print(q)
    print(r)

    while True:
        pass
