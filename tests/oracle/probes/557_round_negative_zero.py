# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# round(-0.0, n) returned 0.0, losing the sign: the `< 0.0` test is false for
# negative zero. The sign is now read off the float's sign bit, so -0.0 keeps its
# sign on both the n>0 and the n<=0 paths, as CPython does.
print(round(-0.0, 2))
print(round(-0.0, -2))
print(round(-0.0, 5))
print("END")
