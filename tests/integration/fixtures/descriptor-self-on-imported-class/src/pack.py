from pymcu.types import uint8


class Field:
    def __init__(self, addr: uint8) -> None:
        self.addr = addr

    def __get__(self, obj, objtype=None) -> uint8:
        return self.addr + obj.base

    def __set__(self, obj, value: uint8) -> None:
        obj.base = value
