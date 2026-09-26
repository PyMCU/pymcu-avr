# machine.disable_irq() returns the state it found and enable_irq(state) restores exactly
# that, so a nested section's restore leaves interrupts off while the outer one is open.
# It used to return 1 whatever the state, and enable_irq(s2) switched them back on
# (PyMCU#353): SREG.I read 1, 0, 0, 1, 1 where upstream gives 1, 0, 0, 0, 1.
import machine
from pymcu.chips.atmega328p import SREG, GPIOR0
from pymcu.hal.irq import enable_interrupts

if GPIOR0.value == 0:
    enable_interrupts()
print(SREG[7])
s1 = machine.disable_irq()
print(SREG[7])
s2 = machine.disable_irq()
print(SREG[7])
machine.enable_irq(s2)
print(SREG[7])
machine.enable_irq(s1)
print(SREG[7])
print("END")
