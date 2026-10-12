# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A property named `value` read on an instance a class holds in a field (`self._c.value`), the
# shape of every driver that keeps its pin in `self._pin`. The read used to take the register
# shortcut for `.value` and answer the field itself, so the getter never ran (adafruit_onewire's
# reset never sampled the line).
_g = 0


def bump() -> None:
    global _g
    _g += 1


class Inner:
    @property
    def value(self) -> int:
        return _g


class Holder:
    def __init__(self) -> None:
        self._c = Inner()

    def get(self) -> int:
        return self._c.value


h = Holder()
print(h.get())
bump()
print(h.get())
bump()
bump()
print(h.get())
print("END")
