# examples/irremote_transmit_simpletest.py from the Adafruit bundle, as published. Upstream
# names board.IR_TX, the pin of an IR LED that the boards carrying one have; the Uno has none,
# so the carrier comes out of D3 (OC2B), the pin the AVR's carrier timer is fixed to.
# SPDX-FileCopyrightText: 2021 ladyada for Adafruit Industries
# SPDX-License-Identifier: MIT
import time

import adafruit_irremote
import board
import pulseio

pulseout = pulseio.PulseOut(board.D3, frequency=38000, duty_cycle=2**15)
encoder = adafruit_irremote.GenericTransmit(
    header=[9500, 4500], one=[550, 550], zero=[550, 1700], trail=0
)

while True:
    encoder.transmit(pulseout, [255, 2, 255, 2])
    time.sleep(0.2)
