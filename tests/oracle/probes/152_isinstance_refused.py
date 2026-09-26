# expect: match
# doc: isinstance() folds at compile time since #424, and the bool it yields prints True, which is
# the half of #386 that works: a bool written straight into print() keeps its True/False text.
# #386 is OPEN -- it is the other half, a bool that arrives through a name binding, a function
# return or an `or`, which prints 1/0. This probe is in the healthy half, so `match` is right.
class Base:
    def __init__(self, v: int) -> None:
        self.v = v
class Sub(Base):
    pass
b = Sub(3)
print(isinstance(b, Base))
print("END")
