# PGO fixture: two @inline expansion sites inside a branch the declared
# workload never runs. `strobe`'s body is too small for the static outlining
# cost model to ever pay (2 sites x 2 words vs 2 calls + body + RET), so only
# a profile that shows the enclosing block cold can outline it -- the profiled
# build must differ from the plain build, and the behaviour must not change.
from pymcu.hal.gpio import Pin
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


@inline
def strobe(led) -> None:
    led.high()
    led.low()


def main():
    led = Pin("PB5", Pin.OUT)
    btn = Pin("PD2", Pin.IN, pull=Pin.PULL_UP)
    uart = UART(9600)

    while True:
        if btn.value() == 1:
            led.high()
            delay_ms(5)
            led.low()
            delay_ms(5)
            uart.write('.')
        else:
            strobe(led)
            strobe(led)
            uart.write('!')
