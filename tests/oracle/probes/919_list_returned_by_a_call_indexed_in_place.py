# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A list returned by a function or a method, indexed on the spot (`make(4)[2]`, `box.build(3)[1]`,
# `make(5)[k + 4]`): the subscript read the list pointer from a name the call never wrote, so it
# answered whatever was in that slot. adafruit_onewire does it with `ow_bus.scan()[0]`.
def make(n: int) -> list[int]:
    xs = []
    for i in range(n):
        xs.append(i * 3)
    return xs


class Box:
    def build(self, n: int) -> list[int]:
        ys = []
        for i in range(n):
            ys.append(i + 10)
        return ys


b = Box()
print(make(4)[2])
print(b.build(3)[1])
k = 0
print(make(5)[k + 4])
print("END")
