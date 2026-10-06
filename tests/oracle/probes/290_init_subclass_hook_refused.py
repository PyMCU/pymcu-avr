# expect: refuse __init_subclass__' is defined, but PyMCU never calls it
# doc: https://docs.pymcu.org/limitations/#classes-and-inheritance
class Base:
    def __init__(self, n):
        self.n = n
    def __init_subclass__(cls):
        return 0
class Child(Base):
    def __init__(self, n):
        self.n = n
c = Child(7)
print(c.n)
print("END")
