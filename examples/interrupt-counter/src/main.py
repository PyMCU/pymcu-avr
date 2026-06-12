# ATmega328P: External interrupt counter — INT0 ISR + UART output
#
# Demonstrates:
#   - Pin.irq(): sets up INT0 (IRQ_FALLING), enables interrupt mask and SEI automatically
#   - ISR<->main signaling through a plain module global: the compiler detects
#     it as ISR-shared (volatile semantics) and promotes it to GPIOR0, so the
#     flag accesses compile to single-cycle OUT/IN — no manual GPIOR idiom
#   - No manual EICRA/EIMSK register writes or asm("SEI") needed
#
# Hardware: Arduino Uno
#   - Button on PD2 (Arduino pin 2 = INT0), active low, with pull-up
#   - LED on PB5 (Arduino pin 13, built-in)
#   - Serial terminal at 9600 baud — receives count byte on each press
#
from pymcu.types import uint8
from pymcu.hal.gpio import Pin
from pymcu.hal.uart import UART

# Set by the ISR, polled and cleared by main. ISR-shared -> auto-promoted
# to GPIOR0; starts at 0 on reset.
pressed: uint8 = 0


def int0_isr():
    # Minimal ISR: set the shared event flag (compiles to OUT on GPIOR0)
    global pressed
    pressed = 1


def main():
    led  = Pin("PB5", Pin.OUT)
    btn  = Pin("PD2", Pin.IN, pull=Pin.PULL_UP)
    uart = UART(9600)

    btn.irq(Pin.IRQ_FALLING, int0_isr)   # configures INT0 + enables SEI

    count: uint8 = 0
    uart.println("INT COUNTER")

    while True:
        if pressed == 1:
            pressed = 0
            count += 1
            led.toggle()
            uart.write(count)        # send raw count byte over UART
