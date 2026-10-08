# expect: match
# doc: docs/rfcs/0014-by-type-not-by-name.md p17
# A user method SPELLED monotonic is not a counter read. Before RFC 0014
# family 7 the driver's AST scan reserved Timer0 for this program anyway
# (the call's attribute name matched its list), arming a live OVF interrupt
# and adding ~490 B the program never asked for. The output was right then
# and must stay right now that the resolved callee decides instead: this
# probe pins the program-visible half of the flip -- the build-log half
# (no millis_init preamble) is covered by the driver token tests.
class Motor:
    def monotonic(self):
        return 1


m = Motor()
print(m.monotonic())
print("END")
