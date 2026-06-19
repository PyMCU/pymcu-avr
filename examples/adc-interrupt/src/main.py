# ATmega328P: ADC interrupt-driven sampling on PC0 (ADC0)
#
# adc.irq(adc_isr) registers the handler at the ADC Complete vector
# (byte 0x002A / word 0x0015), enables ADIE, and sets SEI -- no
# @interrupt decorator or asm("SEI") needed.
#
# The ISR reads ADCL first (latches ADCH), publishes the low byte and a
# done flag through plain module globals. Both are detected as ISR-shared
# (volatile semantics) and auto-promoted to GPIOR registers, so the
# ISR<->main handoff is single-cycle I/O with zero SRAM.
# Main loop prints the result over UART and triggers the next conversion.
#
# Hardware: Arduino Uno
#   - ADC0 = PC0 (analog input)
#   - UART TX 9600 baud
#
from pymcu.types import uint8
from pymcu.chips.atmega328p import ADCL, ADCH
from pymcu.hal.uart import UART
from pymcu.hal.adc import AnalogPin

# Written by the ISR, read by main. ISR-shared -> auto-promoted to GPIORs;
# both start at 0 on reset.
sample: uint8 = 0   # latest ADC low byte
done:   uint8 = 0   # conversion-complete flag


def adc_isr():
    # Must read ADCL first to latch ADCH.
    global sample, done
    sample = ADCL.value
    done = 1


def main():
    uart = UART(9600)
    adc  = AnalogPin("PC0")
    adc.irq(adc_isr)

    uart.println("ADC IRQ")

    # Kick off first conversion
    adc.start_conversion()

    while True:
        if done == 1:
            done = 0
            result: uint8 = sample
            uart.write(result)
            uart.write('\n')
            # Start next conversion
            adc.start_conversion()
