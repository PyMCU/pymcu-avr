# A global an interrupt writes has to survive the handler returning (PyMCU#328).
#
# R2-R15 is the callee-saved home pool the register allocator hands out, and every ISR
# prologue pushes that whole range and the epilogue pops it. A global homed there was
# restored to its pre-interrupt value on RETI: the handler ran, the write happened, and the
# value never moved. A quadrature encoder counted every edge and reported 0 for ever.
#
# Two globals, because the failure needs the allocator to actually pick registers for them:
# a 16-bit counter is never GPIOR-eligible, and a byte-wide one competes for what is left.
#
# The test pulses D2 during the 20 ms wait and reads back:
#   break  GPIOR0             GPIOR1              GPIOR2
#   2      position low byte  position high byte  last state seen
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, EIMSK, EICRA, DDRD, PORTD, PIND
from pymcu.time import delay_ms
from pymcu.types import asm, uint8, uint16, compile_isr

position: uint16 = 0
last: uint8 = 0


def step():
    global position, last
    position = position + 1
    last = PIND.value & 0x04


def on_int0():
    # The handler calls a shared body, which is the ordinary shape when two pins are decoded
    # by the same code -- and what makes the ISR non-leaf, so the prologue saves the whole
    # callee-saved range R2-R11 and the epilogue restores it.
    step()


def main():
    DDRD.value = 0x00
    PORTD.value = 0x04          # pull-up on D2
    EICRA.value = 0x02          # INT0 on the falling edge
    EIMSK.value = 0x01
    compile_isr(on_int0, 0x0002)
    asm("SEI")

    asm("BREAK")                # the test pulses the pin during the wait below
    delay_ms(20)

    GPIOR0.value = uint8(position & 0xFF)
    GPIOR1.value = uint8(position >> 8)
    GPIOR2.value = last
    asm("BREAK")

    while True:
        pass
