# expect: refuse `[row] * H` creates H aliases of ONE row object
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
g = [[0] * 4] * 2
print(g[0][0])
