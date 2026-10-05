# expect: compile
# doc: src/compiler/IR/IRGenerator/Core.cs:1040
# post-beta-1 smoke: a program that is only imports has no top-level
# statements, and the compiler used to synthesize `main` only when some
# statement survived declaration filtering -- the link then failed with
# "undefined reference to `main'". The probe exercises the shape `pymcu
# install` uses to verify a library: an entry file that only imports it.
import time
