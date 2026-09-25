# expect: refuse a 2-D grid cannot be sliced
# doc: docs/language/limitations.md:698
g = [[0] * 4 for _ in range(3)]
s = g[0:2]
print(len(s))
