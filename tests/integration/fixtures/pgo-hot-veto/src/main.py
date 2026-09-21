# PGO fixture: three @inline expansion sites inside the loop the declared
# workload runs forever. `strobe`'s body is large enough for the static
# outlining cost model to prove the win (3 sites x 8 pin ops vs 3 calls +
# body + RET + 1 param), so the plain build outlines it -- and the profile,
# which shows the enclosing loop block hot, must veto that outlining. The
# profiled build differs from the plain build (larger: the region stays
# inline), and the behaviour must not change.
from pymcu.hal.gpio import Pin
from pymcu.hal.uart import UART


@inline
def strobe(led) -> None:
    led.high()
    led.low()
    led.high()
    led.low()
    led.high()
    led.low()
    led.high()
    led.low()


def main():
    led = Pin("PB5", Pin.OUT)
    # 1 Mbaud keeps the UART's TX-ready spin from dominating the profile: at
    # 9600 the wait loop burns ~99% of cycles and the strobe block never
    # reaches the 1% veto threshold.
    uart = UART(1000000)

    while True:
        strobe(led)
        strobe(led)
        strobe(led)
        uart.write('.')
