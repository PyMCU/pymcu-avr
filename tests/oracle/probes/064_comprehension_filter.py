# expect: refuse a list comprehension with a filter (if) is not supported
# doc: https://docs.pymcu.org/limitations/
# #394: a filtered comprehension's length is decided at run time, and PyMCU lays a
# list comprehension out as a fixed array whose length is a compile-time constant.
# Refused with a diagnostic naming the restriction and a workaround (an explicit
# loop) instead of silently filling a wrong-length array.
xs = [x for x in [1, 2, 3, 4] if x > 2]
for v in xs:
    print(v)
print("END")
