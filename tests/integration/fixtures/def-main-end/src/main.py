# The same program-ending shape as module-level-end, but reached through an
# explicit `def main()`. Both shapes splice the same entry function, so falling
# off its end must park the CPU identically: __pymcu_halt, PB5 held high.
from machine import Pin


def main() -> None:
    led = Pin(13, Pin.OUT)
    led.high()
