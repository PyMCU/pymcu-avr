# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import inline
def outer():
    total = 1
    @inline
    def add(n):
        nonlocal total
        total = total + n
    add(4)
    add(5)
    return total
print(outer())
print("END")
