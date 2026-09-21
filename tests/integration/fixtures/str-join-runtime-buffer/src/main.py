# str-join-runtime-buffer: `s = "".join([chr(b) for b in buf])` builds a
# runtime string in SRAM and print() streams it. print()'s string writer is a
# function whose parameter is also named `s`, and the indexed-load path
# resolved that parameter to the module-level `main.s` instead -- the callee
# ignored its argument and emitted the buffer again, so the firmware printed
# "ABC" for every string write and the newline plus "END" never arrived
# (oracle probe 070).
#
# Expected UART output:
#   ABC
#   END
from pymcu.hal.console import print

buf = bytearray(b"ABC")
s = "".join([chr(b) for b in buf])
print(s)
print("END")
