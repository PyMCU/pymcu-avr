# PyMCU -- inline-returned-result: a returned @inline call carries its callee's value
# (PyMCU#302).
#
# Three levels, the shape the AVR PWM HAL had while #300 was being written: a helper whose
# `match` arm returns its own parameter, called in RETURN position from a selector's `match`,
# from a class constructor that stores the result in an annotated local and programs a
# register with it.
#
# The HAL that was measured had lost the helper's `return`, and the prescaler came out as
# whatever the reset prologue left in R16 -- TCCR0B = 0x3F instead of 3, which clocks Timer0
# from the T0 pin so the output never toggles. That shape is now refused at compile time; this
# is the shape that is allowed, and the value has to arrive.
from pymcu.chips.atmega328p import TCCR0A, TCCR0B, OCR0A, DDRD
from pymcu.exceptions import CompileError
from pymcu.hal.console import print
from pymcu.types import uint8, uint16, const, inline


@inline
def code_for(pin: const, code: uint8) -> uint8:
    match pin:
        case "PD6" | "PD5":
            return code
        case _:
            raise CompileError("unsupported pin")


@inline
def select(pin: const, freq: uint16) -> uint8:
    match pin:
        case "PD6" | "PD5":
            if freq > 2762:
                return code_for(pin, 0x02)
            elif freq > 488:
                return code_for(pin, 0x03)
            else:
                return code_for(pin, 0x05)
        case _:
            raise CompileError("unsupported pin")


class Channel:
    def __init__(self, pin: const, duty: uint8, freq: uint16):
        self._pin = pin
        prescaler: uint8 = 0
        prescaler = select(pin, freq)
        DDRD[6] = 1
        OCR0A.value = duty
        TCCR0A.value = TCCR0A.value | 0x83
        TCCR0B.value = prescaler


def main():
    ch = Channel("PD6", 128, 500)
    print("B", TCCR0B.value, "A", TCCR0A.value, "O", OCR0A.value)
    print("END")
