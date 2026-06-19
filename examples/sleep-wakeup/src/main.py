# ATmega328P: Sleep / Power Management demo
#
# Demonstrates:
#   - sleep_idle() from pymcu.hal.power
#   - Pin.irq(): sets up INT0 wake source, enables interrupt mask and SEI automatically
#   - ISR<->main signaling through a plain module global: detected as
#     ISR-shared (volatile semantics) and auto-promoted to GPIOR0
#
# Hardware: button on PD2 (INT0, pull-up), UART at 9600 baud
#
from pymcu.types import uint8
from pymcu.hal.uart import UART
from pymcu.hal.gpio import Pin
from pymcu.hal.power import sleep_idle

# Set by the ISR on wake, polled and cleared by main. ISR-shared ->
# auto-promoted to GPIOR0; starts at 0 on reset.
woke: uint8 = 0


def int0_isr():
    global woke
    woke = 1    # set wakeup flag


def main():
    uart = UART(9600)
    btn  = Pin("PD2", Pin.IN, pull=Pin.PULL_UP)

    uart.println("SLEEP DEMO")

    btn.irq(Pin.IRQ_FALLING, int0_isr)   # configures INT0 + enables SEI

    count: uint8 = 0
    while count < 5:
        uart.println("SLEEP")
        sleep_idle()
        if woke == 1:
            woke = 0
            count += 1
            uart.println("WAKE")

    uart.println("DONE")

    while True:
        pass
