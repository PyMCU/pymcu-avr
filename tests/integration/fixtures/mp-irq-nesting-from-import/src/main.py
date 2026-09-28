# mp-irq-nesting with the functions bound by `from machine import ...`, the spelling most
# MicroPython code uses. Upstream prints 1, 0, 0, 0, 1: the inner enable_irq(s2) restores
# the disabled state and only the outer enable_irq(s1) re-enables.
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
