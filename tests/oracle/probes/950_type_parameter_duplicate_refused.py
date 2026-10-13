# expect: refuse :7:6: error: SyntaxError: PEP 695 type parameters are not supported
# doc: https://github.com/PyMCU/PyMCU/issues/521
# CPython rejects `def f[T, T]` ("duplicate type parameter"); the Python front end used to
# compile it and run it. Both front ends refuse the bracket.
from pymcu.types import uint8

def f[T, T](n: uint8) -> uint8:
    return n

print(f(3))
print("END")
