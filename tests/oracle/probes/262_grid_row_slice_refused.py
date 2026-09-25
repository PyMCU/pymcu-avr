# expect: refuse a row of a 2-D grid cannot be sliced
# doc: docs/language/limitations.md:693
g = [[0] * 4 for _ in range(2)]
s = g[0][1:3]
print(len(s))
