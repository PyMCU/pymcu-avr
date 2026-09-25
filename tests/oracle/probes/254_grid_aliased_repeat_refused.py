# expect: refuse `[row] * H` creates H aliases of ONE row object
# doc: docs/language/limitations.md:679
g = [[0] * 4] * 2
print(g[0][0])
