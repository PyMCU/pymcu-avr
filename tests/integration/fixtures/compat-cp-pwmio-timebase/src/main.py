# CircuitPython pwmio next to the time base (PyMCU#295).
#
# A PWMOut on D6 at the default frequency next to supervisor.ticks_ms(): the clock must
# keep its rate (dt 100 for a 100 ms sleep). Measured on an Arduino Uno, a 5000 Hz PWMOut
# on D6 made monotonic() run 8.44x too fast; that request is now refused at compile time.
import board
import pwmio
import supervisor
import time


def main():
    led = pwmio.PWMOut(board.D6, duty_cycle=32768)
    t0 = supervisor.ticks_ms()
    time.sleep(0.1)
    print("dt", supervisor.ticks_ms() - t0)
    print("END")
