# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for q in range(5): if q == 1: continue` has no break, no return, no raise -- it is
# guaranteed to complete -- but `continue` still needs a label to jump to. A
# materialize/settle fix elsewhere in this round narrowed the range unroll's single
# "does this loop need a break/continue label" flag down to "can this loop exit
# early" (break/return/raise only, continue does not count), and label creation rode
# along with it by mistake: a continue-only loop got no label at all and refused to
# compile with "Continue statement outside of loop". The two questions -- does this
# need a label, can this loop end early -- are answered separately now.
for q in range(5):
    if q == 1:
        continue
print(q)
print("END")
