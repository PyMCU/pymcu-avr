# PyMCU -- array-array-as-list: `import array` / `array.array(typecode)`, read as the
# heap-bounded list[T] the compiler already has (PyMCU#433).
#
# adafruit_dht's shape: a method builds an array.array("H") locally, appends into it in a
# loop, and returns it; a sibling method takes an array.array parameter (no typecode -- the
# element width comes from whatever list the caller actually passes) and reads it with a
# run-time index. Four bugs stood between "import array" and this working, three of them
# silent wrong code rather than refusals:
#
#   1. `array` was a hard ImportError (a Python standard module PyMCU does not implement).
#   2. A `list[T]` parameter on a plain (non-@inline) function was refused outright, on the
#      claim that leaving it alone dropped the function silently -- it does not; `list[T]`
#      is UNKNOWN to StringToDataType exactly like a class-typed parameter, and the same
#      call-site-expansion machinery already resolves it correctly.
#   3. A bare `x = f()` where f's declared return is `list[T]` (or `array.array`, which
#      reaches the same UNKNOWN path) typed x as UNKNOWN-turned-uint8, and a later `x[i]`
#      silently compiled into a bit-test of x's address instead of a list read.
#   4. Even once x was typed as the GC pointer it is, the call RESULT's own Temporary was
#      still allocated 1 byte wide (from that same UNKNOWN default) at the point it was
#      created, and codegen moves bytes by a value's OWN width, not by a name looked up
#      afterwards: the pointer's high byte was silently zeroed on the way into x, which the
#      optimizer's constant folding happened to paper over -- caught by the differential
#      suite (optimized vs `PYMCU_NO_OPT=1`) disagreeing on this exact program.
#
# Expected UART output (CPython's numbers for the same bytes):
#   pulses_sum 18
#   buf_sum 15
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import uint16, uint8
import array


class Pulses:
    def get_pulses(self) -> array.array:
        pulses = array.array("H")
        pulses.append(5)
        pulses.append(6)
        pulses.append(7)
        return pulses

    def get_bytes(self) -> array.array:
        buf = array.array("B")
        buf.append(4)
        buf.append(5)
        buf.append(6)
        return buf

    def total(self, values: array.array, start: uint16, stop: uint16) -> uint16:
        s: uint16 = 0
        i: uint16 = start
        while i < stop:
            s = s + values[i]
            i = i + 1
        return s

    def pulses_sum(self) -> uint16:
        pulses = self.get_pulses()
        return self.total(pulses, 0, 3)

    def bytes_sum(self) -> uint16:
        buf = self.get_bytes()
        return self.total(buf, 0, 3)


uart = UART(9600)

p = Pulses()
print("pulses_sum", p.pulses_sum())
print("buf_sum", p.bytes_sum())
print("done")

while True:
    pass
