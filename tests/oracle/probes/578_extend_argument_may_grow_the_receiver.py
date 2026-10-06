# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# CPython evaluates the whole extend() argument before the receiver grows: grow()
# appends 7 first, then the pinned 8 lands after it, so xs is [1, 7, 8]. Reading
# the logical end before the argument ran put the 8 on top of the 7.

xs = [1]


def grow():
    xs.extend([7])
    return 8


xs.extend([grow()])
print(len(xs), xs[1])
print("END")
