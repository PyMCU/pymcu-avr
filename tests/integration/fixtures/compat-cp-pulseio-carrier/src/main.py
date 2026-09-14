# CircuitPython pulseio.PulseOut: a carrier, gated by a pulse list (pymcu-circuitpython#9).
#
# Timer2 runs fast PWM mode 7 on OC2B (D3), where OCR2A is the carrier period: that is the
# only mode on this part that reaches 38 kHz, because the fixed-TOP modes give 62500, 7812,
# 1953, 976, 488, 244 and 61 Hz and nothing between. send() connects and disconnects the
# compare output, and times the gaps against Timer1.
#
# The test watches the carrier gate (COM2B1 in TCCR2A) and the pin. At the first BREAK the
# timer is programmed and the carrier is OFF; send() runs between the first BREAK and the
# second.
import board
import pulseio
from pymcu.types import asm, uint16

frame: uint16[4] = [560, 560, 1690, 560]


def main():
    out = pulseio.PulseOut(board.D3, frequency=38000, duty_cycle=32768)
    asm("BREAK")
    out.send(frame, 4)
    asm("BREAK")

    while True:
        pass
