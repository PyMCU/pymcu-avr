# ATmega328P: Servo.write(degrees) maps angles to exact pulse widths.
#
# Timer1 mode 14, prescaler 8: tick = 0.5 us, OCR = ticks - 1.
#   write(0)   -> 1000 us -> OCR1A = 1999
#   write(90)  -> 1500 us -> OCR1A = 2999
#   write(180) -> 2000 us -> OCR1A = 3999
# The old degrees*11 approximation lost 0.055 us/degree: write(90) produced
# 1495 us (OCR 2989), measured on a real Uno with a logic analyser. The exact
# map is degrees*100//9.
#
# Sends 'A' after write(0), 'B' after write(90), 'C' after write(180).
from pymcu.hal.servo import Servo
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def main():
    uart = UART(9600)
    s = Servo("PB1")

    s.write(0)
    uart.write('A')
    delay_ms(50)

    s.write(90)
    uart.write('B')
    delay_ms(50)

    s.write(180)
    uart.write('C')

    while True:
        delay_ms(1000)
