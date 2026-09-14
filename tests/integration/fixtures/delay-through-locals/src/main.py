# A delay computed into locals costs what the same arithmetic written out costs (PyMCU#327).
#
# `nap` holds the microseconds and the milliseconds in locals and narrows with a cast;
# `nap_spelled` writes the same arithmetic as one expression of the parameter. The second
# folded through the AST evaluator and reached the calibrated busy loop; the first did not,
# and took the generic counted subroutine -- 60 bytes more, and 974 us where 1000 was asked
# for, for a program whose only difference is a name.
#
# The test reads the firmware: neither call may reach `_delay_ms_avr`, and the two halves must
# be the same number of cycles apart.
from pymcu.time import delay_ms
from pymcu.types import uint16, uint32, inline, asm
from pymcu.chips.atmega328p import GPIOR0


@inline
def nap(seconds: float):
    total_us: uint32 = uint32(seconds * 1000000.0 + 0.5)
    ms: uint32 = total_us // 1000
    if ms != 0:
        delay_ms(uint16(ms))


@inline
def nap_spelled(seconds: float):
    if uint32(seconds * 1000000.0 + 0.5) // 1000 != 0:
        delay_ms(uint16(uint32(seconds * 1000000.0 + 0.5) // 1000))


def main():
    GPIOR0.value = 1
    nap(0.001)
    GPIOR0.value = 2
    asm("BREAK")
    nap_spelled(0.001)
    GPIOR0.value = 3
    asm("BREAK")
    while True:
        pass
