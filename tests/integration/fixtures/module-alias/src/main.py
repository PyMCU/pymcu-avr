# Aliased module imports must resolve to the real module: `import machine as m`
# and `import time as t` used to mangle m.UART / t.sleep_ms to undefined symbols.
# Constructs a UART through the alias and writes a banner; the comma-separated
# import exercises multi-module import parsing too.
import machine as m, time as t


def main():
    uart = m.UART(0, 9600)
    led = m.Pin(13, m.Pin.OUT)
    uart.write("MA\n")
    led.value(1)
    t.sleep_ms(1)
    led.value(0)
    while True:
        pass
