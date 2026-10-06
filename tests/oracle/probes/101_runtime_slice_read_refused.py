# expect: refuse slice
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
def take(n):
    buf = bytearray(b"abcd")
    part = buf[0:n]
    print(part)
take(2)
print("END")
