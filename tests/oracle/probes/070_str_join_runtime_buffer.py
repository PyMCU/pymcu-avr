# expect: match
# doc: docs/language/roadmap.md:60
buf = bytearray(b"ABC")
s = "".join([chr(b) for b in buf])
print(s)
print("END")
