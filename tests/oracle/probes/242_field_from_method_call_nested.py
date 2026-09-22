# expect: match
# doc: docs/language/roadmap.md
# tracked: same root cause as 241 -- an unannotated class method's return type is
# tracked: never inferred (TypeInference sees module-level functions only), so the
# tracked: nested `self.v = self._read()` lays the field out as uint8 and the
# tracked: outlined callee truncates `return 300` to a byte. Nested variant: the
# tracked: write sits inside a `for`, exercising the #488 nested-write walk on top
# tracked: of the missing method-return inference.
# tracked: an unannotated class method's `return 300` truncates to a byte -- nested write variant of 241
class Dev:
    def __init__(self):
        for i in range(1):
            self.v = self._read()
    def _read(self):
        return 300

d = Dev()
print(d.v)
print("END")
