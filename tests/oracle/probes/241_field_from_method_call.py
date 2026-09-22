# expect: match
# doc: docs/language/roadmap.md
# tracked: unannotated class-method return types are never inferred --
# tracked: TypeInference only walks prog.Functions (module level), so `_read`'s
# tracked: `return 300` leaves ReturnType empty; the field `v` then defaults to
# tracked: uint8 and the outlined `Dev__read` subroutine truncates its own return
# tracked: to a byte (prints 0). Fix: run the existing return-join pass over
# tracked: class methods too, feed functionReturnTypes, and let
# tracked: DeriveFieldLayout's self._m() evidence (InferAssignedFieldType,
# tracked: Scan.cs) see the inferred type, not only the declared one (which
# tracked: already works -- see 243).
# tracked: an unannotated class method's `return 300` truncates to a byte -- method return types are never inferred, so `v` prints 0
class Dev:
    def __init__(self):
        self.v = self._read()
    def _read(self):
        return 300

d = Dev()
print(d.v)
print("END")
