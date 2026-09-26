# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/522
# tracked: #522
# A sequence pattern is lowered to a plain list expression, which carries no length test, so
# every arm below is taken whatever the subject's length is. Each arm prints a constant, so
# the phantom-capture read of #401 cannot reach the output: what is measured here is only
# whether the arm was taken.
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
