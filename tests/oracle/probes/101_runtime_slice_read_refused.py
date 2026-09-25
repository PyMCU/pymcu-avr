# expect: refuse slice
# doc: docs/language/limitations.md:663
def take(n):
    buf = bytearray(b"abcd")
    part = buf[0:n]
    print(part)
take(2)
print("END")
