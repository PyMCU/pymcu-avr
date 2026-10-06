# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
class Device:
    SCALE = 5
d = Device()
d.SCALE = 9
print(d.SCALE)
print("END")
