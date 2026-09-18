from pymcu.types import uint8


class Dev:
    ALS_GAIN_1 = 0
    ALS_GAIN_2 = 1
    ALS_GAIN_X = 2
    vals = {ALS_GAIN_2: 2, ALS_GAIN_1: 1, ALS_GAIN_X: 0.25}

    def scaled(self, n: uint8) -> uint8:
        return uint8(self.vals[n] * 100)
