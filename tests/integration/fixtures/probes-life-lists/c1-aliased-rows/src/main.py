# probes-life-lists/c1: `[[0] * W] * H` -- the spelling a CircuitPython user
# reaches for second. In CPython this is H names for ONE row object, so
# `g[0][1] = 1` writes every row. Lowering it as an independent grid would be
# unfaithful, so it is refused with the comprehension spelling shown.
# EXPECTED BUILD FAILURE.
g = [[0] * 4] * 2
print(g[0][0])
