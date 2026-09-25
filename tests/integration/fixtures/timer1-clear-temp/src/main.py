# PyMCU -- timer1-clear-temp: the HAL half of PyMCU#492.
#
# timer1_clear() wrote TCNT1L before TCNT1H, which is the order a 16-bit READ takes, not
# the order a write takes. On AVR the low byte's write is what commits the pair, taking
# the high half from the shared TEMP latch, so clearing low-first committed whatever the
# previous 16-bit access had left in TEMP and the TCNT1H = 0 that followed only refilled
# TEMP for the next access. The counter came out of clear() holding a stale high byte.
#
# timer1_set_compare() leaves the high byte of its argument in TEMP (it writes OCR1AH,
# then OCR1AL), which is exactly the state a real program is in: it configures the
# compare value and then clears the counter. It sets WGM12 but no clock select bits, so
# Timer1 stays stopped and the counter keeps whatever clear() left in it.
#
# Expected UART output:
#   0
#   done
from pymcu.chips.atmega328p import TCNT1
from pymcu.hal.avr.timer import timer1_set_compare, timer1_clear
from pymcu.hal.console import print
from pymcu.types import uint16

timer1_set_compare(0x0B34)
timer1_clear()
# Read back through the register name rather than timer1_counter(), so the fixture
# measures what clear() left in TCNT1 and not whether the HAL's own read is ordered.
c: uint16 = TCNT1.value
print(c)
print("done")
