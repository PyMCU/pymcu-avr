# RFC 0013 (docs/rfcs/0013-memory-model.md, PyMCU-rfc13), phase 0c ("static by
# exclusion"): the minimal three-level pattern the beta-1 blocker generalises
# (busio.I2C._locked, reached as ss.i2c_device.i2c._locked under a module-
# level Seesaw instance -- PyMCU-gemlife-lockinit).
#
# owner (module-level, level 1)
#   .holder  = Holder(shared)   <- built DIRECTLY inside Owner.__init__, not
#                                  passed to Owner's OWN constructor as an
#                                  argument (level 2)
#     .target = shared           <- Holder aliases a PARAMETER, does not build
#                                   it (level 3: the shared Locker instance)
#       .locked                  <- the mutable field that must read 0 at boot
#
# Under phase 0c's rule this needs no proof that `target`/`shared` are the
# "same" object, and no proof this field's write-site is module-rooted: ANY
# home that is not a parameter, local or temporary of some function (RFC 0013
# section 3) is static and zero-initialized at boot, whatever the flattened
# name looks like and whoever else might also flatten the same field under a
# different name. This is what PYMCU_FORCE_POISON_COLD_BOOT=255 exercises.
from pymcu.types import uint8


class Locker:
    def __init__(self):
        self.tag: uint8 = 7
        self.locked: uint8 = 0

    def try_lock(self) -> uint8:
        if self.locked:
            return 0
        self.locked = 1
        return 1


class Holder:
    def __init__(self, target: Locker):
        self.target = target

    def acquire(self) -> uint8:
        return self.target.try_lock()


class Owner:
    def __init__(self, shared: Locker):
        self.holder = Holder(shared)


lock = Locker()
owner = Owner(lock)

acquired: uint8 = owner.holder.acquire()
print(acquired)

while True:
    pass
