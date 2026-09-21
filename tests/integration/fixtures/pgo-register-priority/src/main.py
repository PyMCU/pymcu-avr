# PGO fixture for the backend register-priority consumer (pymcuc-avr --profile).
# 'cold' collects more static uses than 'hot', but every one of them sits in
# setup code that runs once, while 'hot' lives in the loop the workload spins
# on. Under the profile 'hot' wins the lower R2-R15 home, so the profiled image
# differs from the plain one. The optimizer's veto has nothing to veto here --
# no repeated @inline site in a hot block -- so the MIR comes out identical and
# the image difference can only be the backend's work.
from pymcu.hal.gpio import Pin
from pymcu.hal.uart import UART


def main():
    uart = UART(1000000)
    sensor = Pin("PD2", Pin.IN)
    cold: uint8 = 0
    hot: uint8 = 0
    # The pin read defeats constant folding: 'cold' stays a real MIR variable
    # with seven uses, all in the run-once setup block.
    cold = sensor.value()
    cold = cold + 1
    cold = cold * 2
    cold = cold + 3
    cold = cold * 2
    cold = cold + 5
    hot = cold & 3
    while True:
        hot = hot + 1
        if hot == 100:
            uart.write('d')
            hot = 0
