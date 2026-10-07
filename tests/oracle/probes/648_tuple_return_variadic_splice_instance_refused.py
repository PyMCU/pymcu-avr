# expect: refuse instance of 'uname_result'
# doc: https://docs.pymcu.org/limitations/
# os_mod.uname(*()): the *args splice rewrites the CallExpr, so a refusal
# keyed on the element's node identity never sees the dispatch. The produced
# temp itself names the resolved callee's -> uname_result now, so the module
# receiver plus splicing no longer slips an instance into a scalar slot.
import pymcu.os as os_mod


def f():
    return os_mod.uname(*()), 7


a, b = f()
print(bool(a))
print("END")
