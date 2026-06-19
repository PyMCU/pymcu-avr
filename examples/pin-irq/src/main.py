# ATmega328P: Pin.irq() -- INT0 falling-edge interrupt on PD2
#
# Demonstrates:
#   - Pin.irq(Pin.IRQ_FALLING, handler) to configure hardware INT0
#   - compile_isr() intrinsic auto-registers on_press at INT0 vector
#   - No @interrupt decorator required on the handler function
#   - ISR<->main signaling through a plain module global: the compiler
#     detects it as ISR-shared (volatile semantics) and promotes it to
#     GPIOR0, so the ISR write and the main poll compile to single-cycle
#     OUT/IN on an I/O register -- zero SRAM, no manual GPIOR idiom
#   - EICRA[1]=1, EICRA[0]=0 -> falling edge; EIMSK[0]=1 -> INT0 enable
#   - SREG[7]=1 -> global interrupts enabled (sei)
#
# Hardware: Arduino Uno
#   Button on PD2 (Arduino pin 2 = INT0), active low, with pull-up
#   UART TX at 9600 baud
#
# Output:
#   "PIN IRQ\n"    -- boot banner
#   count byte     -- raw byte (1, 2, 3...) sent on each button press
#
from pymcu.types import uint8
from pymcu.hal.gpio import Pin
from pymcu.hal.uart import UART

# Set by the ISR, polled and cleared by main. ISR-shared -> auto-promoted
# to GPIOR0 (always-volatile I/O storage); starts at 0 on reset.
pressed: uint8 = 0


def on_press():
    # Minimal ISR: set the shared flag (compiles to OUT on GPIOR0)
    global pressed
    pressed = 1


def main():
    btn  = Pin("PD2", Pin.IN, pull=Pin.PULL_UP)
    uart = UART(9600)

    # Configure INT0 for falling-edge trigger and enable global interrupts.
    # compile_isr() inside pin_irq_setup automatically places on_press at
    # the INT0 vector -- no @interrupt decorator needed.
    btn.irq(Pin.IRQ_FALLING, on_press)

    count: uint8 = 0
    uart.println("PIN IRQ")

    while True:
        if pressed == 1:
            pressed = 0
            count = count + 1
            uart.write(count)
