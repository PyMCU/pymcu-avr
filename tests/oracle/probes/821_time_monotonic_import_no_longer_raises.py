# expect: match
# doc: lib/src/pymcu/time.py
# `from time import monotonic` (and monotonic_ns) used to be an ImportError:
# pymcu.time had neither name, unlike CPython (both) or CircuitPython
# (monotonic only). This probe compiling and running at all is the test --
# both names now exist and are callable.
from time import monotonic, monotonic_ns

a = monotonic()
b = monotonic_ns()
print(a >= 0.0)
print(b >= 0)
print("END")
