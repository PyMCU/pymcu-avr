# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
buf = bytearray(b"ABC")
s = "".join([chr(b) for b in buf])
print(s)
print("END")
