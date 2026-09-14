# CircuitPython time.sleep() with a literal duration costs one calibrated delay and no
# arithmetic (pymcu-circuitpython#26).
#
# The rewrite that took sleep() past 65 s and under a millisecond first counted a local
# down in a while loop, and every program with a sleep(1) carried __mul32 and __div32
# (the servo program went from 66 to 924 bytes). The test reads the assembly: no 32-bit
# helper, no generic delay subroutine, only the calibrated constant loops.
import board
import digitalio
import time

out = digitalio.DigitalInOut(board.D6)
out.direction = digitalio.Direction.OUTPUT

while True:
    out.value = True
    time.sleep(0.001)
    out.value = False
    time.sleep(0.25)
    out.value = True
    time.sleep(0.0005)
    out.value = False
    time.sleep(1)
