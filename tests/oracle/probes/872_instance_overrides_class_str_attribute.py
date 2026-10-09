# expect: match
# doc: https://docs.pymcu.org/roadmap/
# An instance that overrides a str class attribute through a method keeps reading its
# own text; the class default (and every OTHER instance) stays unaffected. This is the
# instance>class precedence the new class-level fallback (StaticStringOfField /
# TryGetCompileTimeText) must preserve: it is only tried AFTER the instance's own
# flattened-storage lookup fails. CPython prints "mine", "mine", "override", "mine".
class Fake:
    platform = "mine"

    def override(self):
        self.platform = "override"


f = Fake()
g = Fake()
print(f.platform)
print(Fake.platform)
f.override()
print(f.platform)
print(g.platform)
