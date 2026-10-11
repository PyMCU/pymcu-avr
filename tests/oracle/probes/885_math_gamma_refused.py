# expect: refuse math.gamma is not provided
# doc: https://docs.pymcu.org/limitations/
# CPython's math has names a float32 target cannot honestly carry. They are defined so that
# using one says why, at the call, instead of "Unknown module member".
import math

print(math.gamma(5.0))
print("END")
