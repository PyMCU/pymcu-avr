# CircuitPython's busio.UART timeout is seconds for EVERY numeric spelling --
# upstream coerces it with mp_obj_get_float, so timeout=1 is one second and the
# property reads back 1.0. The layer used to read ints as milliseconds, and the
# getter answered the raw field: print(uart.timeout) said 1 while the timeout
# itself was 1 ms. CircuitPython answers 1.0 -- and that is what prints here.
import board
from busio import UART

uart = UART(board.TX, board.RX, baudrate=115200, timeout=1)
print(uart.timeout)
uart.timeout = 0.25
print(uart.timeout)
