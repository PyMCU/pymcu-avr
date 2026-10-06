# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
buf = bytearray(b"abcdef")
print(buf[1:5:2])
print("END")
