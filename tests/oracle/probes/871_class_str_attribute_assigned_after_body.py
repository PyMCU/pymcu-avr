# expect: match
# doc: https://docs.pymcu.org/roadmap/
# A str class attribute attached AFTER the class body closes (`Fake.platform = "mine"`,
# a monkey-patch CPython and MicroPython both allow): the class scan never saw it, so no
# mutableGlobals slot was ever reserved for `Fake_platform` the way a class-body
# attribute's always is. The literal-string write path (Assign.cs's `ClassName.attr =
# value`) still records the TEXT in strConstantVariables, but with no slot behind
# "Fake_platform" the class-level attribute lookup (TryFindClassAttributeFromClass) could
# not even see the name existed, so a read through an instance fell all the way to the
# undefined-attribute fallback: a fabricated, never-written variable reading 0. CPython
# prints "mine" four times.
class Fake:
    pass


Fake.platform = "mine"
f = Fake()
print(f.platform)
print(Fake.platform)
x = f.platform
print(x)
y = Fake.platform
print(y)
