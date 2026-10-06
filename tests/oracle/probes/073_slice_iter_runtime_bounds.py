# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def total(n):
    buf = bytearray(b"abcdef")
    s = 0
    for b in buf[1:n]:
        s = s + b
    return s
print(total(4))
print("END")
