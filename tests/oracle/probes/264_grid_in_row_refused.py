# expect: refuse 'in' on a row of a 2-D grid
# doc: docs/language/limitations.md:674
g = [[0] * 4 for _ in range(2)]
if 1 in g[0]:
    print("found")
print("END")
