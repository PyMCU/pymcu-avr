# expect: refuse a list comprehension of class instances is not supported
# doc: https://docs.pymcu.org/limitations/
# #394: a comprehension used directly as a for-loop's own iterable (not assigned to
# a name first) used to fall through every recognized for-in shape and report the
# generic "iterable must be ..." refusal, which does not name what is actually
# unsupported here (PyMCU lays a class instance out at compile time with no array
# slot to live in). Now gives the same specific diagnostic VisitExpression already
# gives the same comprehension reaching a plain value position.
class Pin:
    def __init__(self, n):
        self.n = n
    def read(self):
        return self.n + 1
for p in [Pin(n) for n in (2, 3, 4)]:
    print(p.read())
print("END")
