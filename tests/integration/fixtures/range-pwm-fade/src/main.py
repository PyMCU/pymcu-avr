# PyMCU -- range-pwm-fade: the CircuitPython PWM fade, written with range() (PyMCU#284)
#
# The shape every CircuitPython fade example has: a percentage walked up with
# range(0, 101) and down with range(100, -1, -1), scaled to the 16-bit duty_cycle of
# pwmio.PWMOut. Both loops fit 8 bits, so they worked before the counter was sized from
# its bounds; this fixture pins that they keep working and that the duty reaches the
# hardware: OCR0A holds the high byte of duty_cycle at two checkpoints (asm BREAK).
#
#   checkpoint 1: p == 99 on the way up,   duty 64879 -> 253 counts high, OCR0A 252
#   checkpoint 2: p == 50 on the way down, duty 32767 -> 128 counts high, OCR0A 127
#
# Expected UART (115200):
#   u 0 0
#   u 1 655
#   u 50 32767
#   u 99 64879
#   u 100 65535
#   d 100 65535
#   d 50 32767
#   d 0 0
#   END
import board
import pwmio
from pymcu.types import asm, uint16

MAX_DUTY = 65535


def main():
    led = pwmio.PWMOut(board.D6, frequency=5000, duty_cycle=0)
    duty: uint16 = 0

    for p in range(0, 101):
        duty = int((p / 100) * MAX_DUTY)
        led.duty_cycle = duty
        if p == 0 or p == 1 or p == 50 or p == 99 or p == 100:
            print("u", p, duty)
        if p == 99:
            asm("BREAK")

    for p in range(100, -1, -1):
        duty = int((p / 100) * MAX_DUTY)
        led.duty_cycle = duty
        if p == 100 or p == 50 or p == 0:
            print("d", p, duty)
        if p == 50:
            asm("BREAK")

    print("END")

    while True:
        pass


main()
