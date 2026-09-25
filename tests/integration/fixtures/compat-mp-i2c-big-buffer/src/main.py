# machine.I2C.writeto with a buffer longer than 255 bytes.
#
# The AVR I2C entry point declared its byte count uint8, so len(buf) truncated at the
# call: a 300-byte buffer went out as 300 & 0xFF == 44 bytes, and a 512-byte one -- the
# framebuffer of a 128x32 SSD1306 -- went out as zero, so show() put a single byte on the
# bus and the display stayed blank while every command before it was correct (PyMCU#511).
#
# Nothing in the source says 300: the count comes from len(), so the compiler's refusal of
# a literal that does not fit its parameter cannot see it either.
#
# Protocol:
#   Boot: "READY\n"
#   Cmd 'W' (0x57): writeto(0x48, big) -- sends all 300 bytes; echoes 'W' when done
#
# The buffer is filled with a position-dependent pattern so a short write and a write of
# the wrong bytes are told apart by the recorder: buf[i] == (i * 7 + 1) & 0xFF.

from machine import I2C, UART
from pymcu.types import uint8, uint16


def main():
    uart = UART(0, 9600)
    i2c = I2C()
    uart.write("READY\n")

    big = bytearray(300)
    i: uint16 = 0
    while i < 300:
        big[i] = uint8((i * 7 + 1) & 0xFF)
        i = i + 1

    while True:
        cmd: uint8 = uart.read()
        if cmd == 87:
            i2c.writeto(0x48, big)
            uart.write(87)
