# expect: divergence https://docs.pymcu.org/limitations/#exception-handling
# doc: https://docs.pymcu.org/limitations/#exception-handling
# MicroPython semantics PyMCU implements: a one-argument integer raise sets
# e.errno, and print(e) renders [Errno n] NAME from the errno table (the bare
# number when the code is unknown). CPython leaves .errno unset on OSError(n)
# and str() is just the number -- the ERR/MSG line tags mark the divergent
# values so the registered transform can map them.
try:
    raise OSError(110)
except OSError as e:
    print("ERR", e.args[0], e.errno)
    print("MSG", e)

try:
    raise TimeoutError(115)
except OSError as e:
    print("ERR", e.args[0], e.errno)
    print("MSG", e)

try:
    raise OSError(42)
except OSError as e:
    print("ERR", e.args[0], e.errno)
    print("MSG", e)

print("END")
