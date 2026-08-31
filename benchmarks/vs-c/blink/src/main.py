from pymcu.boards.arduino_uno import LED_BUILTIN
from pymcu.hal.gpio import Pin
from pymcu.time import delay_ms


def main():
    led = Pin(LED_BUILTIN, Pin.OUT)
    while True:
        led.high()
        delay_ms(500)
        led.low()
        delay_ms(500)
