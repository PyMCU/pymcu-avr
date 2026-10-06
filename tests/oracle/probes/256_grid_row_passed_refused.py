# expect: refuse names a row of a 2-D grid -- a view into the flat array, not a list value
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
def take(r):
    return r[0]

g = [[0] * 4 for _ in range(2)]
take(g[1])
