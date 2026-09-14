# PyMCU -- compat-cp-pwmio-frequency: pwmio.PWMOut.frequency reprograms the timer
#
# Constructed with variable_frequency=True, the setter has to change the Timer0 prescaler
# (TCCR0B) to the nearest achievable frequency, the way the constructor's frequency= does.
# It used to store the value and leave the timer alone.
#
# Checkpoints (asm BREAK), each with TCCR0B expected:
#   1 -- frequency=1000 at construction   -> prescaler 64  (0x03)
#   2 -- pwm.frequency = 20000            -> prescaler 8   (0x02)
#   3 -- pwm.frequency = 100              -> prescaler 1024 (0x05)
#   4 -- pwm.frequency = 50000            -> prescaler 1   (0x01)
import board
import pwmio
from pymcu.types import asm


def main():
    pwm = pwmio.PWMOut(board.D6, duty_cycle=32768, frequency=1000, variable_frequency=True)
    asm("BREAK")
    pwm.frequency = 20000
    asm("BREAK")
    pwm.frequency = 100
    asm("BREAK")
    pwm.frequency = 50000
    asm("BREAK")
    while True:
        pass


main()
