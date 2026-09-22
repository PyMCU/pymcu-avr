# probes-life-lists/c2: `take(g[1])` passes a row where a value is expected --
# inside the callee there is no row object, only the flat array.
# EXPECTED BUILD FAILURE.
def take(r):
    return r[0]

g = [[0] * 4 for _ in range(2)]
take(g[1])
