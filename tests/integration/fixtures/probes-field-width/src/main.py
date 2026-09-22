# probes-field-width -- pins a silent field-width truncation.
#
# `self.rng = seed` binds the field to an unannotated __init__ parameter, whose
# type is empty, so the field layout leaves it at the uint8 default. The only
# other write is INSIDE a nested loop in seed(), which DeriveFieldLayout
# (src/compiler/IR/IRGenerator/Scan.cs) never visits -- it scans top-level
# method statements only -- so neither the widening pass nor the field-kind
# conflict diagnostic ever sees the 31-bit value the field must hold. The
# result: a one-byte rng, `rng * 1103515245 + 12345` keeps only byte 0, and
# `(rng >> 16) & 3` reads bits that are always zero, so every cell seeds live.
#
# CPython prints the seeded world's first four cells: 0 0 1 0.
# The AVR firmware prints 1 1 1 1 -- the pinned symptom of the truncation.
SEED = 20260921


class Life:
    def __init__(self, width, height, seed):
        self.width = width
        self.height = height
        self.cells = bytearray(width * height)
        self.next_cells = bytearray(width * height)
        self.rng = seed

    def seed(self):
        for y in range(self.height):
            for x in range(self.width):
                self.rng = (self.rng * 1103515245 + 12345) & 0x7FFFFFFF
                self.cells[y * self.width + x] = (
                    1 if ((self.rng >> 16) & 3) == 0 else 0
                )


life = Life(32, 8, SEED)
life.seed()
for i in range(4):
    print(life.cells[i])
