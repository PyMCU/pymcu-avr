# probes-life-lists/c6: `[[0] * cols() for _ in range(8)]` -- the row width is
# a call result, not a compile-time constant, so the flat array's size is
# unknown. EXPECTED BUILD FAILURE.
def cols():
    return 32

g = [[0] * cols() for _ in range(8)]
print(g[0][0])
