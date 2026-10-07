# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# CPython builds (bump(), bump(), bump()) EAGERLY: all three calls happen,
# in order, before the loop -- or its body's `break` -- ever runs. Evaluating
# a run-time element lazily, right before its own unrolled iteration, let an
# earlier iteration's `break` skip a later element's call entirely -- the
# third bump() was never emitted, so the counter it bumps read one short
# (1 2 2 instead of 3 3 3).
counter = bytearray([0])


def bump() -> int:
    counter[0] = counter[0] + 1
    return counter[0]


for v in (bump(), bump(), bump()):
    print(counter[0])
    if v == 1:
        continue
    break
else:
    print(99)
print(counter[0])
print("END")
