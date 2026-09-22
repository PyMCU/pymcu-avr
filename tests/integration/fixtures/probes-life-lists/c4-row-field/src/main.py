# probes-life-lists/c4: `b.slot = g[1]` stores a row in a field -- the field
# would hold a view into the flat array, not a value. EXPECTED BUILD FAILURE.
class Box:
    def __init__(self):
        self.slot = 0

g = [[0] * 4 for _ in range(2)]
b = Box()
b.slot = g[1]
