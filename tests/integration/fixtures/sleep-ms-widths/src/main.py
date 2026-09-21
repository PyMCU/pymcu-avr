# PyMCU -- sleep-ms-widths: a constant time.sleep_ms()/sleep_us() lowers to a
# calibrated busy loop whose counter is as narrow as the iteration count allows:
# 1, 2, 3 or 4 registers at 3, 4, 5 or 6 cycles per iteration. Pin 13 toggles
# around every sleep so a test can measure each pulse width in cycles and the
# loop register widths can be read off the listing.
#
# Checkpoints (a BREAK before and after every sleep):
#   1->2   sleep_ms(500)    ~  8 000 000 cycles  (3 registers)
#   3->4   sleep_ms(1)      ~     16 000 cycles  (2 registers)
#   5->6   sleep_ms(10)     ~    160 000 cycles  (2 registers)
#   7->8   sleep_ms(2000)   ~ 32 000 000 cycles  (3 registers)
#   9->10  sleep_us(10)     ~        160 cycles  (1 register)
#  11->12  sleep_ms(6000)   ~ 96 000 000 cycles  (4 registers)
from machine import Pin
import time
from pymcu.types import asm

led = Pin(13, Pin.OUT)

led.toggle()
asm("BREAK")
time.sleep_ms(500)
asm("BREAK")
led.toggle()
asm("BREAK")
time.sleep_ms(1)
asm("BREAK")
led.toggle()
asm("BREAK")
time.sleep_ms(10)
asm("BREAK")
led.toggle()
asm("BREAK")
time.sleep_ms(2000)
asm("BREAK")
led.toggle()
asm("BREAK")
time.sleep_us(10)
asm("BREAK")
led.toggle()
asm("BREAK")
time.sleep_ms(6000)
asm("BREAK")
led.toggle()

while True:
    pass
