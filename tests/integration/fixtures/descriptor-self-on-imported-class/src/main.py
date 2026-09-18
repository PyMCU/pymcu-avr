# descriptor-self-on-imported-class: Adafruit INA219.bus_voltage shape.
#
# A @property on an imported class reads self.raw, which is a descriptor.
# The rewrite of type(self).raw.__get__(self, type(self)) used the mangled
# class key (sensor_Dev) as a VariableExpr, which is not a bound name.
# That is what stopped adafruit_ina219 after value: Any had a width, and
# adafruit_veml7700's self.light_gain in gain_value.
#
# WHAT DISCRIMINATES: 22. Field(9) + base 2, times 2. A compile that still
# named the mangled class would not build.
from pymcu.time import delay_ms
from sensor import Dev

d = Dev(2)


def main():
    while True:
        print(d.volts)
        print("END")
        delay_ms(1200)
