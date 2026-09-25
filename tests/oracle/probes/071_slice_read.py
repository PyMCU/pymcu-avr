# expect: match
# doc: docs/language/roadmap.md:61
buf = bytearray(b"abcdef")
print(buf[1:5:2])
print("END")
