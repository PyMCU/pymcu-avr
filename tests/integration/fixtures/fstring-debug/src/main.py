# fstring-debug: the '=' debug spelling of an f-string (issue #185).
#
# `f"{seed=}"` is written to label a value. The label used to be dropped on the
# way through the parser, with no diagnostic, so the program printed a bare
# number and the log line still looked like a log line. Every value here is
# chosen so its digits cannot be mistaken for the label.
#
# Expected UART output:
#   DBG
#   seed=17
#   seed = 17
#   count=250 seed=17
#   add(seed, 22)=39
#   seed=11
#   a=b
from pymcu.types import uint8
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def add(a: uint8, b: uint8) -> uint8:
    return a + b


def main():
    uart = UART(9600)
    uart.println("DBG")

    seed: uint8 = 17
    count: uint8 = 250

    print(f"{seed=}")
    print(f"{seed = }")
    print(f"{count=} {seed=}")
    print(f"{add(seed, 22)=}")
    print(f"{seed=:02x}")
    print(f"{'a=b'}")

    while True:
        delay_ms(1000)
