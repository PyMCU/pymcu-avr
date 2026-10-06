# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import uint8, inline

def inner(buf: bytearray, v: uint8):
    buf[0] = v
    buf[1] = v + 1

@inline
def pump(buf: bytearray, a: uint8):
    inner(buf, a)
    inner(buf, a + 1)
    inner(buf, a + 2)
    inner(buf, a + 3)
    inner(buf, a + 4)
    inner(buf, a + 5)

x = bytearray(8)
y = bytearray(8)
pump(x, 10)
pump(y, 40)
print(x[0], x[1])
print(y[0], y[1])
print("END")
