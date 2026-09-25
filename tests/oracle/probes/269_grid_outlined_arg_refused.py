# expect: refuse a 2-D grid cannot be passed to an outlined function
# doc: docs/language/limitations.md:674
g = [[0] * 4 for _ in range(2)]

def read(g2):
    return g2[0][0]

print(read(g))
