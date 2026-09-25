# CircuitPython onewireio: the 1-Wire reset, whose LOW pulse the test times.
#
# The protocol fixes the two numbers this measures. The master drives the line low for at
# least 480 us to reset every device on the bus, releases it, samples for a presence pulse
# about 70 us later, and then leaves the line alone long enough to complete a 480 us
# recovery -- `onewireio.OneWire.reset` asks for 410 us of that.
#
# Nothing is attached to the pin, so no device answers. What is under test is the width of
# the pulse the MASTER drives, which it drives whether or not anything is listening; a
# scripted device would add a second driver of the same line and make the edges harder to
# attribute, not easier.
#
# The result of reset() is deliberately NOT printed. Sampling a floating input with nothing
# on it is the one observable here that depends on the simulator's pull-up model rather than
# on the program, and the optimizer differential axis caught it at once: the faster build
# samples a few cycles earlier and read 0 where the unoptimized build read 1. That is the
# same mechanism the axis already tolerates for `examples/dht-sensor`, and a fixture with no
# unmodelled input needs no tolerance at all.
#
# This fixture exists because there was none. `surfacecov-ds18x20` is the only other thing
# that reaches this module and it is build-refused on purpose, so until now the CircuitPython
# 1-Wire reset had never been compiled to firmware, let alone timed (PyMCU#501).
#
# The pin is D2 (PD2), the one the native ds18b20 driver uses.
import board
import onewireio
from pymcu.types import asm

bus = onewireio.OneWire(board.D2)


def main():
    asm("BREAK")
    bus.reset()
    asm("BREAK")

    while True:
        pass
