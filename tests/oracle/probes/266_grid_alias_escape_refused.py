# expect: refuse a row of a 2-D grid is a view into the flat array, not a value
# doc: docs/language/limitations.md:693
g = [[0] * 4 for _ in range(2)]

def take(r):
    return r[0]

r = g[1]
take(r)
