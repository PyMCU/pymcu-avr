# compat-cp-value-toggle: reading digitalio's .value goes through the getter.
#
# `.value` is also how a ptr[T] is dereferenced, and the pointer lowering used to
# claim every member named value -- so on a DigitalInOut it handed back the
# instance instead of reading the pin. `led.value = not led.value`, the blink
# every CircuitPython tutorial teaches, then left the LED stuck with no error.
#
# Expected UART output:
#   a=1
#   b=0
#   c=1
#   d=1
import board
import digitalio
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def main():
    uart = UART(9600)
    led = digitalio.DigitalInOut(board.D13)
    led.direction = digitalio.Direction.OUTPUT

    led.value = 1
    delay_ms(1)
    uart.write_str("a=")
    uart.print_byte(led.value)

    led.value = not led.value
    delay_ms(1)
    uart.write_str("b=")
    uart.print_byte(led.value)

    led.value = not led.value
    delay_ms(1)
    uart.write_str("c=")
    uart.print_byte(led.value)

    v = led.value
    uart.write_str("d=")
    uart.print_byte(v)

    while True:
        pass
