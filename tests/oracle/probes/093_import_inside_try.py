# expect: match
# doc: https://docs.pymcu.org/limitations/#slices
try:
    from pymcu.types import uint8
except ImportError:
    print(0)
else:
    print(uint8(260))
print("END")
