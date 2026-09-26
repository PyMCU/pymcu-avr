# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/521
# frontend: py-parser
# tracked: #521
# A PEP 695 type parameter opens a scope and binds its name inside the body, so CPython reads
# the parameter here and not the module global. The Python front end never reads the
# type_params field, so the bracket vanishes and the body reads the global instead. The value
# is the discriminator: printing the parameter's own repr is what tells the two apart.
from pymcu.types import uint8

T = 5

def which[T]() -> uint8:
    return T

print(which())
print("END")
