# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for x, y in zip(a, b):` where a and b are names bound to constant tuples --
# ResolveConstSequenceExpr's own Bind closure folds each element at compile time only,
# with no backing store. Both names also cleared their fold every iteration (a side
# that binds a real element, not a fold, still has to win over a PRIOR iteration's
# stale one), so nothing ever committed either fold as a real write: a read after the
# loop saw whatever was there before it ran, not the pair in progress when the break
# fired.
a = (1, 2, 3)
b = (4, 5, 6)
for x, y in zip(a, b):
    break
print(x)
print(y)
print("END")
