# ATmega328P: UART at 115200 must use U2X0 double speed.
#
# UBRR=8 at 16x oversampling is 111111 baud (-3.5%): transmit survives because
# the receiving end resynchronizes on every start bit, but the AVR's own
# receiver accumulates the error across the frame and drops every byte on real
# silicon (input() hung forever; verified on a real Uno, fixed with U2X0=1 and
# UBRR=16 = 115942 baud, +0.64%). The emulator does not model baud mismatch,
# so this fixture pins the register configuration instead.
#
# Sends 'U' at 115200 after init.
from pymcu.hal.uart import UART


def main():
    uart = UART(115200)
    uart.write('U')
    while True:
        pass
