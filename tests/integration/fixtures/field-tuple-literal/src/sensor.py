from pymcu.types import uint8


class Dev:
    def __init__(self):
        self.scale = (10, 20, 30, 40, 50, 60, 70, 80)

    def at(self, n: uint8) -> uint8:
        return self.scale[n]
