# expect: match
# doc: docs/rfcs/0014-by-type-not-by-name.md p17
# One probe for every name on the family-7 token list a user may collide with:
# ticks_ms, ticks_us, micros, monotonic_ns as methods, and millis_init /
# clock_init as program functions. The spellings are the ones the source scans
# reserved Timer0 (or skipped an injection) on; the resolved bindings are none
# of the runtime's. The program's own millis_init() is a no-op here and must
# not satisfy [TIMEBASE_INIT] for a counter the program never reads either --
# output is the same either way, so the oracle pins the semantic parity and
# the build-log pins live in the driver/stdlib token tests.
class Clock:
    def ticks_ms(self):
        return 3

    def ticks_us(self):
        return 4

    def micros(self):
        return 5

    def monotonic_ns(self):
        return 6

    def millis_init(self):
        print("user millis_init")

    def clock_init(self):
        print("user clock_init")


c = Clock()
c.millis_init()
c.clock_init()
print(c.ticks_ms())
print(c.ticks_us())
print(c.micros())
print(c.monotonic_ns())
print("END")
