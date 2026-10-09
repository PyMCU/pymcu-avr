# expect: match
# doc: docs/rfcs/0014-by-type-not-by-name.md p17
# A user method SPELLED millis is not a counter read. `millis` joined the
# family-7 timebase-reader list (alongside micros/ticks_ms/ticks_us/monotonic/
# monotonic_ns) so that a bare millis() call auto-arms Timer0 the way its
# siblings already did -- the list is still keyed on the RESOLVED callee, not
# the spelling, so a user's own method named millis reports nothing and stays
# unreserved, the same parity 680 already pins for a method named monotonic.
class Clock:
    def millis(self):
        return 7


c = Clock()
print(c.millis())
print("END")
