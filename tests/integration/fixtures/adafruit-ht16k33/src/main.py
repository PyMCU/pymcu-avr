# Init + digit-write probe for the unmodified Adafruit HT16K33 driver's
# Seg7x4 path -- the demandant for compile-time instance-field constants:
# `self._buffer`/`self._buffer_size` bound inside a super().__init__ chain and
# read through nested @inline expansions (_put, _adjusted_index, show), class
# attribute POSITIONS read through `self` via the MRO, `self._chardict` None
# short-circuiting `and`, single-char loop-variable text reaching char.lower()
# and ord() through stacked inline hops, and `isinstance(display,
# segments.Seg7x4)` folded through a module-qualified candidate.
#
# The operation list is the deterministic half of examples/
# ht16k33_segments_simpletest.py (test01) wrapped in a retry loop the way
# test03 brackets it: the simpletest's sleeps and the closing marquee() are
# dropped because marquee() paces itself by time.monotonic(), which would
# compare host seconds against emulated milliseconds -- the transaction COUNT
# would drift between the oracle and the firmware. The constructor stays
# outside the loop so `display` is bound once, exactly as the simpletest has
# it; everything the loop emits is a pure function of the program text, so
# CPython and the firmware produce the same stream. The loop runs twice and
# the program ends, so the firmware emits exactly the oracle's transaction
# stream -- nothing in it sleeps, so a recorder tail would otherwise keep
# capturing further iterations.
import board
import busio

from adafruit_ht16k33 import segments

i2c = busio.I2C(board.SCL, board.SDA)
display = segments.Seg7x4(i2c)

for _ in range(2):
    try:
        display.fill(0)
        display.print(42)
        display.print_hex(0xFF23)
        display.print("12:30")
        display.colon = False
        display[0] = "1"
        display[1] = "2"
        display[2] = "A"
        display[3] = "B"
        if isinstance(display, segments.Seg7x4):
            display.set_digit_raw(0, 0xFF)
            display.set_digit_raw(1, 0b11111111)
            display.set_digit_raw(2, 0x79)
            display.set_digit_raw(3, 0b01111001)
    except Exception as e:
        print("no ack:", e)
