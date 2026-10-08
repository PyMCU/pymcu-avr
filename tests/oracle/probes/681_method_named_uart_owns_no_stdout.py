# expect: match
# doc: docs/rfcs/0014-by-type-not-by-name.md p16
# A user method SPELLED UART is not a UART construction. Before RFC 0014
# family 7 the driver's AST scan read `w.UART()` as "the program owns a
# UART" and staged the imports-only preamble, leaving print() with no
# initialized transmitter -- and it still printed, because the console
# writers fell back to the default device. The token path reports the
# resolved constructor instead, so this program now gets the full stdout
# preamble it always needed. The output was `hi` by accident then; it is
# `hi` by right now.
class Wifi:
    def UART(self):
        pass


w = Wifi()
w.UART()
print("hi")
