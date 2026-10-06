# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
print(int.from_bytes(b"\x34\x12", "little"))
print(int.from_bytes(b"\x12\x34", "big"))
print("END")
