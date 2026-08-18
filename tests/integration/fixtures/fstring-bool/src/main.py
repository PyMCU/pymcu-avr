# fstring-bool: a bool interpolates as Python spells it -- True/False, not 1/0.
#
# A name bound to True/False everywhere it is written is a real bool, so both
# f"{flag}" and print(flag) stream the words. The frontier: a comparison is an
# integer in PyMCU (not a bool), and so is a name that ever holds one, so those
# keep printing digits.
#
# Expected UART output:
#   BOOL
#   flag=True off=False
#   lit=True/False
#   True False
#   toggled=False
#   cmp=1 mixed=0
#   seen=False
#   seen=True
from pymcu.types import uint8
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def main():
    uart = UART(9600)
    uart.println("BOOL")

    flag = True
    off = False
    print(f"flag={flag} off={off}")
    print(f"lit={True}/{False}")
    print(flag, off)

    flag = False
    print(f"toggled={flag}")

    x: uint8 = 3
    cmp = x > 1
    mixed = False
    mixed = x - 3
    print(f"cmp={cmp} mixed={mixed}")

    i: uint8 = 0
    while i < 2:
        seen = False
        if i == 1:
            seen = True
        print(f"seen={seen}")
        i = i + 1

    while True:
        delay_ms(1000)
