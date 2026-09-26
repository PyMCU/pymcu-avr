# expect: refuse f-string
# doc: docs/language/limitations.md:149
# An f-string is supported streamed to a sink and assigned to a name, and refused in other
# expression positions. A `return` is one of them, and so is an instance field; probe 102
# covers a call argument and 423 a ternary arm.
#
# Four positions, four probes, deliberately: the refusal surface is per POSITION, not per
# construction, so a single probe naming one position would leave the others as the kind of
# assumed coverage this sweep exists to stop.
from pymcu.chips.atmega328p import GPIOR0

base = 3 + GPIOR0.value


def label():
    return f"v{base}"


print(label())
print("END")
