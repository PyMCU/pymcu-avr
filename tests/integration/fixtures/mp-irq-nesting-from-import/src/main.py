# mp-irq-nesting with the functions bound by `from machine import ...`, the spelling most
# MicroPython code uses. It prints 1, 0, 0, 1, 1 today where upstream gives 1, 0, 0, 0, 1:
# the compiler takes the result of an @inline function imported with `from` for an instance
# of a class, so `state != 0` inside restore folds to true and enable_irq(s2) re-enables.
# That is a compiler fault (branch fix/bytes-literal-and-from-import-print of PyMCU), not
# this layer's; the test pins today's output so it goes red when the fix lands.
from machine import disable_irq, enable_irq
from pymcu.chips.atmega328p import SREG, GPIOR0
from pymcu.hal.irq import enable_interrupts

if GPIOR0.value == 0:
    enable_interrupts()
print(SREG[7])
s1 = disable_irq()
print(SREG[7])
s2 = disable_irq()
print(SREG[7])
enable_irq(s2)
print(SREG[7])
enable_irq(s1)
print(SREG[7])
print("END")
