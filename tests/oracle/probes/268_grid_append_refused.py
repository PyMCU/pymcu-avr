# expect: refuse a 2-D grid is a flat fixed array -- it has no 'append()'
# doc: docs/language/limitations.md:679
g = [[0] * 4 for _ in range(2)]
g.append([0] * 4)
