# The CircuitPython blink `pymcu new --board attiny85 --stdlib circuitpython` scaffolds,
# copied byte for byte (pymcu-circuitpython#3, #4).
#
# It used to be written board.LED and could not be built for the part it was generated
# for: a bare ATtiny85 has no LED soldered to it, so the layer defines none. PB0 is the
# pin the scaffolder picks now -- free, and not PB5, which is RESET and needs the
# RSTDISBL fuse, after which the chip can no longer be programmed over ISP.
#
# board.D0 is the same leg under the Dn numbering these parts gained with #4; the fixture
# blinks both in turn so the test can see that the two spellings reach the same bit.
import board
import digitalio
import time

# attiny85 has no on-board LED. Wire one to this pin, or change it.
led = digitalio.DigitalInOut(board.PB0)
led.direction = digitalio.Direction.OUTPUT
other = digitalio.DigitalInOut(board.D1)
other.direction = digitalio.Direction.OUTPUT
while True:
    led.value = True
    other.value = True
    time.sleep(0.5)
    led.value = False
    other.value = False
    time.sleep(0.5)
