# ATmega328P: Timer1 overflow interrupt -- ~1 Hz LED blink using Timer.irq()
#
# Demonstrates:
#   - Timer(n, prescaler): zero-cost timer ZCA, prescaler folded at compile time
#   - Timer.irq(handler): registers handler at the OVF vector via compile_isr;
#     no @interrupt decorator or manual TIMSK/SEI writes needed
#   - ISR<->main signaling through a plain module global: detected as
#     ISR-shared (volatile semantics) and auto-promoted to GPIOR0, so the
#     flag accesses compile to single-cycle OUT/IN -- no manual GPIOR idiom
#
# Hardware: Arduino Uno
#   - LED on PB5 (Arduino pin 13, built-in) -- blinks every ~1 second
#   - Serial terminal at 9600 baud -- prints 'T\n' on each toggle
#
# Timer1 (16-bit) at prescaler 256, F_CPU = 16 MHz:
#   overflow period = 65536 * 256 / 16_000_000 ~= 1.049 s
#
from pymcu.types import uint8
from pymcu.hal.gpio import Pin
from pymcu.hal.uart import UART
from pymcu.hal.timer import Timer

# Set by the ISR, polled and cleared by main. ISR-shared -> auto-promoted
# to GPIOR0; starts at 0 on reset.
tick: uint8 = 0


def on_overflow():
    # Minimal ISR: set the shared flag (compiles to OUT on GPIOR0)
    global tick
    tick = 1


def main():
    led  = Pin("PB5", Pin.OUT)
    uart = UART(9600)

    # Timer1 at prescaler 256; irq() enables TOIE1 + SEI, registers on_overflow
    # at the Timer1 OVF vector automatically -- no @interrupt decorator needed.
    timer = Timer(1, 256)
    timer.irq(on_overflow)

    uart.println("TIMER1 IRQ BLINK")

    while True:
        if tick == 1:
            tick = 0
            led.toggle()
            uart.write('T')
            uart.write('\n')
