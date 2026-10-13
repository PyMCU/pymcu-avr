# expect: refuse :10:10: error: SyntaxError: PEP 695 type parameters are not supported
# doc: https://github.com/PyMCU/PyMCU/issues/521
# A PEP 695 type parameter opens a scope and binds its name inside the body, so CPython reads
# the parameter here and not the module global. The Python front end used to drop the bracket
# and the body read the global; both front ends now refuse the bracket where it opens.
from pymcu.types import uint8

T = 5

def which[T]() -> uint8:
    return T

print(which())
print("END")
