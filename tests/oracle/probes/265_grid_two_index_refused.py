# expect: refuse a subscript with more than one index
# doc: docs/language/limitations.md:693
g = [[0] * 4 for _ in range(2)]
print(g[1, 2])
