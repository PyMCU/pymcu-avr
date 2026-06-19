# ATmega328P: ISR-shared plain globals — volatile semantics + GPIOR promotion
#
# Two plain uint8 module globals shared between the INT0 ISR and main:
#   flag    — set by the ISR, polled and cleared by main (classic ISR flag)
#   presses — incremented by the ISR, only read by main (lifetime counter)
#
# No GPIOR0[*] idiom, no @interrupt decorator, no manual volatile handling:
# the compiler detects both globals as ISR-shared (isrSharedGlobals in the
# .mir) and the AVR backend promotes them to GPIOR0/GPIOR1, so the ISR and
# the polling loop talk through 1-cycle IN/OUT I/O registers instead of SRAM.
#
# UART output (9600 baud):
#   Boot:        "VOLATILE\n"
#   Each press:  presses byte (1, 2, 3...) + '\n'
#
from pymcu.types import uint8
from pymcu.hal.gpio import Pin
from pymcu.hal.uart import UART

flag:    uint8 = 0
presses: uint8 = 0


def on_press():
    global flag, presses
    flag = 1
    presses = presses + 1


def main():
    btn  = Pin("PD2", Pin.IN, pull=Pin.PULL_UP)
    uart = UART(9600)

    btn.irq(Pin.IRQ_FALLING, on_press)

    uart.println("VOLATILE")

    while True:
        if flag == 1:
            flag = 0
            uart.write(presses)
            uart.write('\n')
