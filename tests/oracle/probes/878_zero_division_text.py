# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# The text of a ZeroDivisionError the program catches and prints is CPython 3.14's: every
# division operator says "division by zero", and a zero base to a negative power has its own.
def quot(a, b):
    return a // b


def rem(a, b):
    return a % b


def true_div(a, b):
    return a / b


def fdiv(a: float, b: float) -> float:
    return a / b


def fpow(a: float, b: float) -> float:
    return a ** b


try:
    quot(8, 0)
except ZeroDivisionError as e:
    print(e)
try:
    rem(8, 0)
except ZeroDivisionError as e:
    print(e)
try:
    true_div(8, 0)
except ZeroDivisionError as e:
    print(e)
try:
    fdiv(8.0, 0.0)
except ZeroDivisionError as e:
    print(e)
try:
    fpow(0.0, -1.0)
except ZeroDivisionError as e:
    print(e)
print("END")
