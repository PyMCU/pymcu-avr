# expect: refuse a union of instance types is not supported
# doc: docs/language/limitations.md
# RFC 0009 decision 4: instances are storage, not values a tag byte can switch
# between -- a union of class instances is refused, naming the member.
from typing import Union


class A:
    def __init__(self) -> None:
        self.x = 1


class B:
    def __init__(self) -> None:
        self.y = 2


def f(k: int) -> Union[A, B]:
    return A()


print("END")
