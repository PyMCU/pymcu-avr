# machine.UART.readline() and readinto() integration test fixture
#
# readline(buf, max_len) -- reads until '\n'; caller provides buffer (PyMCU deviation)
# readinto(buf)          -- reads at most len(buf) bytes, waiting `timeout` ms for the first
#                           and `timeout_char` ms between them (matches MicroPython; it used
#                           to block until the buffer was full, which a board at the default
#                           timeout of 0 does not do, so the UART is built with timeouts)
#
# Protocol (command-driven loop):
#   Boot: "READY\n"
#   Cmd 'L' (0x4C): readline -> send count byte, then count bytes
#   Cmd 'I' (0x49): readinto -> echo the 3 received bytes

from machine import UART
from pymcu.types import uint8


def main():
    uart = UART(0, 9600, timeout=100, timeout_char=10)
    uart.write("READY\n")

    line_buf: uint8[16] = bytearray(16)
    data_buf: uint8[3] = [0, 0, 0]

    while True:
        cmd: uint8 = uart.read()
        if cmd == 76:
            n: uint8 = uart.readline(line_buf)
            uart.write(n)
            j: uint8 = 0
            while j < n:
                uart.write(line_buf[j])
                j = j + 1
        if cmd == 73:
            uart.readinto(data_buf)
            k: uint8 = 0
            while k < 3:
                uart.write(data_buf[k])
                k = k + 1
