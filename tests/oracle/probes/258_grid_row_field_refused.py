# expect: refuse names a row of a 2-D grid -- a view into the flat array, not a list value
# doc: docs/language/limitations.md:698
class Box:
    def __init__(self):
        self.slot = 0

g = [[0] * 4 for _ in range(2)]
b = Box()
b.slot = g[1]
