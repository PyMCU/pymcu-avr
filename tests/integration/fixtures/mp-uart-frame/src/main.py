# machine.UART takes upstream's frame, buffer writes and read timeouts.
#   UART(0, 9600, bits=7, parity=1, stop=2) -> UCSR0C = 0x3C (7 data, odd, 2 stop)
#   init(9600, 8, None, 1)                    -> 8N1, 0x06 (upstream keeps
#                                             the omitted fields; the layer needs
#                                             the frame spelt out, so it is written)
#   write(bytearray) sends the buffer and returns its length
#   readinto(buf, n) waits `timeout` ms for the first byte, `timeout_char` for the next,
#   and returns how many arrived: the test sends two bytes after "RX", so 2, not a hang.
from machine import UART
from pymcu.chips.atmega328p import UCSR0C, GPIOR0

u = UART(0, 9600, bits=7, parity=1, stop=2, timeout=200, timeout_char=20)
c1 = UCSR0C.value
u.init(9600, 8, None, 1)
print(c1, UCSR0C.value)
out = bytearray(3)
out[0] = GPIOR0.value + 0x41
out[1] = GPIOR0.value + 0x42
out[2] = GPIOR0.value + 0x0A
cnt = u.write(out)
u.flush()
print(cnt)
print("RX")
inb = bytearray(4)
got = u.readinto(inb, 3)
print(got, inb[0], inb[1])
print("END")
