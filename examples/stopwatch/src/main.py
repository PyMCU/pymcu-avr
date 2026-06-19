# ATmega328P: Three-ISR stopwatch
#
# THREE simultaneous ISRs:
#   ISR 1: INT0  (PD2, byte 0x0002) - Start/Stop toggle (falling edge)
#   ISR 2: INT1  (PD3, byte 0x0004) - Reset (falling edge)
#   ISR 3: TIMER0_OVF (byte 0x0020) - Tick counter (~16.384ms per tick)
#
# 61 ticks ~= 1 second. Main loop tracks elapsed seconds and sends them
# over UART as a raw uint8 byte + '\n' whenever seconds increments.
#
# Each ISR signals main through its own plain module global. All three are
# detected as ISR-shared (volatile semantics) and auto-promoted to
# GPIOR0/1/2, so every flag access is single-cycle I/O with zero SRAM.
#
# Hardware: Arduino Uno
#   Start/Stop button: PD2 (Arduino pin 2), active low, pull-up
#   Reset button:      PD3 (Arduino pin 3), active low, pull-up
#   LED:               PB5 (Arduino pin 13, built-in) - on while running
#   UART TX on PD1 at 9600 baud
#
# UART output:
#   Boot: "STOPWATCH\n"
#   On each second increment while running: raw byte = seconds mod 256, '\n'
#   On reset: sends 0, '\n'
#
from pymcu.types import uint8, uint16
from pymcu.chips.atmega328p import PORTB, DDRB
from pymcu.chips.atmega328p import TCCR0B, TIMSK0
from pymcu.hal.gpio import Pin
from pymcu.hal.timer import Timer
from pymcu.hal.uart import UART

# One flag per ISR, polled and cleared by main. ISR-shared -> auto-promoted
# to GPIOR registers; all start at 0 on reset.
tick_f:  uint8 = 0   # Timer0 OVF
start_f: uint8 = 0   # INT0 start/stop
reset_f: uint8 = 0   # INT1 reset


def timer0_ovf_isr():
    global tick_f
    tick_f = 1


def int0_isr():
    global start_f
    start_f = 1


def int1_isr():
    global reset_f
    reset_f = 1


def main():
    DDRB[5] = 1

    # Timer0: normal mode, prescaler 1024 — OVF every 65536/16MHz*1024 ~= 4.096ms
    # Using Timer directly for the OVF interrupt registration
    timer = Timer(0, 1024)
    timer.irq(timer0_ovf_isr)          # registers at TIMER0_OVF vector + SEI

    # INT0 (PD2): start/stop on falling edge
    # INT1 (PD3): reset on falling edge
    btn_start = Pin("PD2", Pin.IN, pull=Pin.PULL_UP)
    btn_reset  = Pin("PD3", Pin.IN, pull=Pin.PULL_UP)

    btn_start.irq(Pin.IRQ_FALLING, int0_isr)
    btn_reset.irq(Pin.IRQ_FALLING, int1_isr)

    uart = UART(9600)
    uart.println("STOPWATCH")

    ticks:   uint8  = 0
    seconds: uint16 = 0
    running: uint8  = 0

    while True:
        # Handle INT0: toggle start/stop
        if start_f == 1:
            start_f = 0
            if running == 0:
                running = 1
                PORTB[5] = 1   # LED on while running
            else:
                running = 0
                PORTB[5] = 0

        # Handle INT1: reset
        if reset_f == 1:
            reset_f = 0
            ticks   = 0
            seconds = 0
            running = 0
            PORTB[5] = 0
            uart.write(0)
            uart.write('\n')

        # Handle Timer0 tick while running
        if tick_f == 1:
            tick_f = 0
            if running == 1:
                ticks += 1
                if ticks >= 61:
                    ticks = 0
                    seconds += 1
                    uart.write(seconds & 0xFF)
                    uart.write('\n')
