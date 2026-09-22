# field-width-nested-write -- PyMCU#488 reproducer, kept green.
#
# `self.rng = seed` binds a field to an unannotated __init__ parameter, whose
# empty type leaves the field at the uint8 default. The only other write is the
# 31-bit LCG update inside the nested `for` loops of seed() -- a write
# DeriveFieldLayout (src/compiler/IR/IRGenerator/Scan.cs) used to miss because
# it scanned top-level method statements only, so the field stayed a byte and
# every store kept byte 0 only.
#
# The scan now walks whole method bodies with the shared statement visitor, so
# the nested write joins the field width and self.rng is uint32. The printed
# answers are CPython's, running the same program:
#   rng        = 666838957  (a uint8 field truncates every store: prints 173)
#   rng >> 16  = 10175      (a uint8 field reads the high bytes as 0)
#   rng & 0xFF = 173        (byte 0 matches either way -- the LCG's low byte
#                            depends only on the previous low byte)
SEED = 20260921


class Life:
    def __init__(self, width, height, seed):
        self.width = width
        self.height = height
        self.rng = seed

    def seed(self):
        for y in range(self.height):
            for x in range(self.width):
                self.rng = (self.rng * 1103515245 + 12345) & 0x7FFFFFFF
        print(self.rng)


life = Life(4, 3, SEED)
life.seed()
print(life.rng >> 16)
print(life.rng & 0xFF)
