# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def walk(xs: list[int]):
    for i, v in enumerate(xs):
        print(i * 10 + v)


def walk_from(xs: list[int], first: int):
    for i, v in enumerate(xs, first):
        print(i * 10 + v)


data: list[int] = []
data.append(7)
data.append(8)
walk(data)
walk_from(data, 2)
walk([1, 2, 3])
print("END")
