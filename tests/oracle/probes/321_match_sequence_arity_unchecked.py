# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/522
# Fixed alongside #401 (same root cause: a sequence pattern's subject with no real array
# storage, a pure compile-time constant sequence, read arraySizes instead of
# ResolveConstSequence for its length, so the arity check never ran). Each arm prints a
# constant, so the phantom-capture read of #401 cannot reach the output: what is measured
# here is only whether the arm was taken.
short = [7, 8]
match short:
    case [a, b, c]:
        print(11)
    case _:
        print(22)

exact = [7, 8, 9]
match exact:
    case [d, e, f]:
        print(33)
    case _:
        print(44)

big = [7, 8, 9, 10]
match big:
    case [g, h, i]:
        print(55)
    case _:
        print(66)
print("END")
