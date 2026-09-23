# expect: match
# doc: isinstance() folds at compile time since #424; the bool it yields prints True now (#386 closed)
class Base:
    def __init__(self, v: int) -> None:
        self.v = v
class Sub(Base):
    pass
b = Sub(3)
print(isinstance(b, Base))
print("END")
