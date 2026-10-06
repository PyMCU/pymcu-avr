# expect: refuse __new__' is defined, but PyMCU never calls it
# doc: https://docs.pymcu.org/limitations/#classes-and-inheritance
class Cached:
    def __new__(cls, n):
        return 0
    def __init__(self, n):
        self.n = n
c = Cached(5)
print(c.n)
print("END")
