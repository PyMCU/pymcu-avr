# expect: compile
# doc: docs/rfcs/0014-by-type-not-by-name.md p7
# The name is not a literal at the callsite -- it is a module-level constant the
# compiler resolves anyway, so [EMBED] carries "main.py" and the driver stages
# the file. The source scan this replaced only ever matched open("literal")
# spellings, so under it this program failed to compile; the accepted change
# is that a compile-time-resolved name embeds the same. main.py is the file
# the harness always writes, so the embed target exists under sources/.
name = "main.py"
f = open(name)
print("opened")
print("END")
