# str.join lowering: the all-constant form folds at compile time, and the
# canonical bytes-to-string idiom ''.join([chr(b) for b in buf]) becomes a
# runtime string (NUL-capped buffer copy) that print() streams by length.
#
# Expected UART output (9600 baud): "ab-cd" newline "Hola" newline "D".
from pymcu.hal.uart import UART
from pymcu.types import uint8


def main():
    uart = UART(9600)
    parts = "-".join(["ab", "cd"])
    buf: bytearray = bytearray(4)
    buf[0] = 72
    buf[1] = 111
    buf[2] = 108
    buf[3] = 97
    s = "".join([chr(b) for b in buf])
    uart.write_str(parts)
    uart.write(10)
    i: uint8 = 0
    while i < 4:
        uart.write(s[i])
        i = i + 1
    uart.write(10)
    uart.write('D')
