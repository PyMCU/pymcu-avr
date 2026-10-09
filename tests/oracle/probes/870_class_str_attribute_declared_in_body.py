# expect: match
# doc: https://docs.pymcu.org/roadmap/
# A str class attribute declared in the class body, read through an instance and through
# the class itself. StaticStringOfField (the compile-time text extractor print() and a
# string assignment both ask) only ever tried the INSTANCE's own flattened storage
# (`f_platform`) -- nothing an instance never wrote to. The class body's own default is
# filed under the CLASS's key (`Fake_platform`) instead, by a different code path
# (RecordClassAttrInit/Scan.cs), so the text lookup missed it and fell through to the
# generic numeric writer, which streamed the attribute's interned string id. CPython
# prints "mine" four times.
class Fake:
    platform = "mine"


f = Fake()
print(f.platform)
print(Fake.platform)
x = f.platform
print(x)
y = Fake.platform
print(y)
