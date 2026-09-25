# expect: match
# doc: docs/language/limitations.md:679
class Grid:
    def __init__(self, width, height):
        self.width = width
        self.height = height
        self.g = [[0] * width for _ in range(height)]

    def fill(self, v):
        for y in range(self.height):
            for x in range(self.width):
                self.g[y][x] = v + y

    def total(self):
        t = 0
        for y in range(self.height):
            for x in range(self.width):
                t = t + self.g[y][x]
        return t


g = Grid(4, 3)
g.fill(2)
print(g.g[1][2])
print(g.total())
print("END")
