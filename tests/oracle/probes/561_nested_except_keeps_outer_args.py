# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# A bound exception read the single live __exn_arg0 word: a raise caught inside
# the handler overwrote it, so outer.args[0] answered the INNER raise's argument
# (2 for the caught OSError(5)). The matched handler now snapshots the delivered
# record and bound reads answer the snapshot; a bare raise or `raise e` restores
# it before propagating.
try:
    raise OSError(5)
except OSError as outer:
    try:
        raise ValueError(2)
    except ValueError:
        pass
    print(outer.args[0])
    print(len(outer.args))
print("END")
