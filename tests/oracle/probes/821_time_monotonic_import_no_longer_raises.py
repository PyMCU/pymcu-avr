# expect: match
# doc: lib/src/pymcu/time.py
# `from pymcu.time import monotonic` (and monotonic_ns) used to be an
# ImportError: pymcu.time had neither name, unlike CPython (both) or
# CircuitPython (monotonic). This probe compiling and running at all is
# the test -- both names now exist and are callable. The qualified
# "pymcu.time" spelling (rather than the bare "time" alias) is deliberate:
# the oracle's CPython half replaces pymcu.time wholesale with a fixed,
# deterministic stub (install_cpython_shims), the same as millis/micros --
# the bare "time" alias would resolve to CPython's own real, unstubbed
# time module instead, which answers monotonic()/monotonic_ns() too but
# would not be exercising the stub this probe is pinning.
from pymcu.time import monotonic, monotonic_ns

a = monotonic()
b = monotonic_ns()
print(a >= 0.0)
print(b >= 0)
print("END")
