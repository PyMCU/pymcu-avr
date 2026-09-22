# probes-life-lists/c7: `r = g[y]` then `take(r)` -- the row alias escapes the
# block as a call argument; a row is a view, not a value to pass.
# EXPECTED BUILD FAILURE.
g = [[0] * 4 for _ in range(2)]

def take(r):
    return r[0]

r = g[1]
take(r)
