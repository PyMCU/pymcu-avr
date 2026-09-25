# PyMCU -- timer16-store-order: PyMCU#483 / PyMCU#492.
#
# A 16-bit timer register on AVR is reached through one shared TEMP latch. Writing the
# HIGH byte only fills TEMP; writing the LOW byte commits both halves at once. So a
# 16-bit store has to put the high byte out FIRST, and a read has to take the LOW byte
# first (which is what latches the high one).
#
# The compiler emitted the store the other way round, low byte first, on both paths: the
# constant store was split into byte copies in the IR, and the runtime store reached the
# AVR backend as one 16-bit copy that lowered to STS low, STS high. Either way the pair
# was committed with whatever TEMP held, and the high byte that followed only refilled
# TEMP for the NEXT access. Seeding TCNT1 with 0x1234 read back 0x0334.
#
# The comparison runs on the chip and every value is printed, so a swapped pair, a
# single-byte write and a read that only took one half all show up as a wrong number
# rather than as a missing line.
#
# Timer1 is left stopped (TCCR1B = 0 out of reset), so TCNT1 keeps what it is given.
#
# Expected UART output:
#   4660
#   22136
#   4660
#   done
from pymcu.chips.atmega328p import TCNT1, OCR1A, ICR1, GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint16

# Constant store: split into byte halves by the IR generator.
# The read-back goes through a uint16 local because `print(TCNT1.value)` reads only the
# low byte today (PyMCU#493), which would hide the high half this fixture is about.
TCNT1.value = 0x1234
a: uint16 = TCNT1.value
print(a)

# A second constant store, to a different pair, right after the first: the value that
# leaks through a stale TEMP is the previous pair's high byte, so ordering the halves
# wrong here cannot be hidden by a TEMP that happens to hold zero.
OCR1A.value = 0x5678
b: uint16 = OCR1A.value
print(b)

# Runtime store: one 16-bit copy that the AVR backend orders. GPIOR0 reads 0 out of
# reset, so the value is 0x1234 with the compiler unable to fold it.
v: uint16 = uint16(GPIOR0.value) + 0x1234
ICR1.value = v
c: uint16 = ICR1.value
print(c)

print("done")
