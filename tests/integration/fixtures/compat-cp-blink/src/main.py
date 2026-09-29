# pymcu.org's own canonical CircuitPython blink -- verbatim from README.md's
# "pitch in one table" section (PyMCU repo root, "Write code you already know" ->
# CircuitPython) and from the Pico-under-CircuitPython comment in that snippet.
#
# Pinned here by name so the README's own size table has something concrete to
# point at, the same way compat-mp-blink-toggle pins the website's MicroPython
# number. Before fix/b1-size this built to 178 B: 30 bytes of unhandled-exception
# report machinery (__pymcu_exn_tail, __pymcu_print_exn_msg, and their two flash
# strings) for a message no reachable raise on this program ever recorded --
# `import board` pulls in busio.I2C/SPI/UART for board.I2C() etc, and busio's
# error helpers raise OSError with a message that used to be counted just for
# existing in an imported-but-uncalled function.
#
# CircuitPython's digitalio.Direction.OUTPUT setter clears PORT before setting
# DDR (the CircuitPython spec requires it), so the fixed number is 148 B: 2 bytes
# more than the native-HAL/MicroPython blink (146 B), not 32.
#
# board.LED, a single DigitalInOut instance, single-instance fold applies.
import board
import digitalio
import time

led = digitalio.DigitalInOut(board.LED)
led.direction = digitalio.Direction.OUTPUT

while True:
    led.value = True
    time.sleep(0.5)
    led.value = False
    time.sleep(0.5)
