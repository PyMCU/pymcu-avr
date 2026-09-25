# expect: match
# doc: docs/language/limitations.md:715
try:
    from pymcu.types import uint8
except ImportError:
    print(0)
else:
    print(uint8(260))
print("END")
