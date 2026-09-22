# probes-life-lists/c3: `return g[y]` hands the caller a row view -- there is
# no row object to return. EXPECTED BUILD FAILURE.
g = [[0] * 4 for _ in range(2)]

def pick(y):
    return g[y]

print(pick(1))
