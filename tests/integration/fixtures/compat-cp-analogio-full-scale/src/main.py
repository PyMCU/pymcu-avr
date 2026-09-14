# CircuitPython analogio: value covers the whole 16-bit range, and a read goes to the
# channel the AnalogIn was built with (pymcu-circuitpython#21).
#
# AnalogIn.value scaled the 10-bit reading by 64, so full scale on the pin read 65472:
# 63 counts short of full scale in the number. A caller turning value into volts was
# always low and `value == 65535` never happened. The HAL now replicates the top bits
# into the bottom ones, which maps 1023 onto exactly 65535.
#
# Two AnalogIn on different channels share one ADMUX, so the read has to re-select. Read
# back at each BREAK:
#   GPIOR0 = low byte of value   GPIOR1 = high byte   GPIOR2 = ADMUX
#
#   break  what                        ADMUX
#   1      both constructed            0x43  (A3 was constructed last)
#   2      a0.value                    0x40  (re-selected channel 0)
#   3      a3.value                    0x43  (re-selected channel 3)
import board
from analogio import AnalogIn
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, ADMUX
from pymcu.types import asm, uint8, uint16


def main():
    a0 = AnalogIn(board.A0)
    a3 = AnalogIn(board.A3)
    GPIOR2.value = ADMUX.value
    asm("BREAK")

    v: uint16 = a0.value
    GPIOR0.value = uint8(v & 0xFF)
    GPIOR1.value = uint8(v >> 8)
    GPIOR2.value = ADMUX.value
    asm("BREAK")

    w: uint16 = a3.value
    GPIOR0.value = uint8(w & 0xFF)
    GPIOR1.value = uint8(w >> 8)
    GPIOR2.value = ADMUX.value
    asm("BREAK")

    while True:
        pass
