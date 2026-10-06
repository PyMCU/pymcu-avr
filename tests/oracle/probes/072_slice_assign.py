# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
buf = bytearray(b"abcdef")
buf[1:4] = b"XYZ"
print(buf)
print("END")
