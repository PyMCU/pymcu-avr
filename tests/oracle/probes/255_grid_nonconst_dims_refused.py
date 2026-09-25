# expect: refuse a grid's dimensions must be compile-time constants
# doc: docs/language/limitations.md:698
def cols():
    return 4

g = [[0] * cols() for _ in range(3)]
print(g[0][0])
