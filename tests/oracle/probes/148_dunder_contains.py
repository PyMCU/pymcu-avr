# expect: match
# doc: docs/language/roadmap.md:41
# `x in <name bound to an instance>` dispatches __contains__ -- pinned by
# LinuxNotInABoundUname and the dunder-methods/outline-dunders fixtures;
# the refusal this probe was written for predates that feature (334d8bef).
class Box:
    def __init__(self, v):
        self.v = v
    def __contains__(self, item):
        return item == self.v
b = Box(5)
print(5 in b)
print("END")
