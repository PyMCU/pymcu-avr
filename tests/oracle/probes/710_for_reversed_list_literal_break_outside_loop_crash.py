# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for v in reversed([1, 2, 3]):` with a break used to refuse to compile at all --
# "Break statement outside of loop" -- because this unroll never pushed anything onto
# the loop stack a break looks for. With that fixed, a read after the loop also needs
# the fold's materialize/settle every other compile-time form has: the fold here lives
# under a bare key, while a read written AFTER the loop resolves through the qualified
# name instead, so the real write has to land there too.
for v in reversed([1, 2, 3]):
    break
print(v)
print("END")
