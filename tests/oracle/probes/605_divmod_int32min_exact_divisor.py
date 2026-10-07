# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# divmod(-2147483648, -2): the dividend being negative proves nothing about the
# divisor -- -1 must be reachable inside b's range for the a / -1 overflow to
# threaten. A constant -2 has range [-2, -2], so the pair compiles and CPython's
# answer (1073741824, 0) is what the firmware prints.
q, r = divmod(-2147483648, -2)
print(q)
print(r)
