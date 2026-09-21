using System.Text.Json;
using PyMCU.Backend.Targets.AVR;
using PyMCU.Common.Models;
using PyMCU.IR;
using Xunit;

namespace PyMCU.UnitTests;

public class AvrBlockMapTests
{
    private static readonly DeviceConfig Atmega328p = new() { Chip = "atmega328p", Arch = "avr" };

    private static (string asm, JsonElement map) Compile(ProgramIR program)
    {
        program.Device ??= ChipCatalog.For(Atmega328p.Chip);
        var codegen = new AvrCodeGen(Atmega328p);
        var mapPath = Path.Combine(Path.GetTempPath(), $"pgo-bm-{Guid.NewGuid():N}.json");
        codegen.EmitBlockMapPath = mapPath;
        var sw = new StringWriter();
        codegen.Compile(program, sw);
        using var doc = JsonDocument.Parse(File.ReadAllText(mapPath));
        File.Delete(mapPath);
        return (sw.ToString(), doc.RootElement.Clone());
    }

    private static ProgramIR MakeProgram(string name, params Instruction[] body)
    {
        var prog = new ProgramIR();
        prog.Functions.Add(new Function { Name = name, Body = body.ToList() });
        return prog;
    }

    private static List<JsonElement> Blocks(JsonElement map) =>
        map.GetProperty("Blocks").EnumerateArray().ToList();

    private static List<JsonElement> Branches(JsonElement map) =>
        map.GetProperty("Branches").EnumerateArray().ToList();

    [Fact]
    public void BlockMap_RecordsEveryEmittedMirLabelVerbatim()
    {
        // main: a loop head label, a conditional jump, an exit label. Both labels
        // are real branch targets, so both survive the peephole to the .asm.
        var prog = MakeProgram("main",
            new Label("L_10"),
            new Copy(new Constant(1), new Variable("x")),
            new JumpIfNotZero(new Variable("x"), "L_20"),
            new Jump("L_10"),
            new Label("L_20"),
            new Return(new Constant(0)));

        var (asm, map) = Compile(prog);

        // Every MIR Label node lands in the map with its name verbatim, plus one
        // entry record per emitted function (main only). The peephole is free to
        // delete or retarget labels afterwards -- such entries resolve to
        // WordAddr=null post-link, which is the documented contract.
        var blocks = Blocks(map);
        var labels = blocks.Where(b => !b.GetProperty("Entry").GetBoolean())
                           .Select(b => b.GetProperty("Label").GetString())
                           .ToList();
        Assert.Equal(new[] { "L_10", "L_20" }, labels);
        Assert.Contains(blocks, b =>
            b.GetProperty("Entry").GetBoolean() &&
            b.GetProperty("Function").GetString() == "main" &&
            b.GetProperty("Label").GetString() == "main");

        // Referenced MIR labels appear verbatim as asm symbols so the post-link
        // resolver can find them in the ELF symtab.
        Assert.Contains("L_10:", asm);

        // One conditional jump recorded: taken=L_20, fallthrough=L_20 (the next
        // boundary after the jump is the exit label).
        var branches = Branches(map);
        var br = Assert.Single(branches);
        Assert.Equal("L_20", br.GetProperty("Taken").GetString());
        Assert.Equal("L_20", br.GetProperty("Fallthrough").GetString());
        Assert.Equal("main", br.GetProperty("Function").GetString());
        // The _pgob_N marker symbol lands in the asm so post-link resolution can
        // read its real address from the ELF symtab.
        var sym = br.GetProperty("Sym").GetString()!;
        Assert.StartsWith("_pgob_", sym);
        Assert.Contains($"{sym}:", asm);
    }

    [Fact]
    public void BlockMap_NoMarkersWithoutFlag()
    {
        var prog = MakeProgram("main",
            new Label("L_10"),
            new JumpIfNotZero(new Variable("x"), "L_20"),
            new Label("L_20"),
            new Return(new Constant(0)));

        prog.Device ??= ChipCatalog.For(Atmega328p.Chip);
        var codegen = new AvrCodeGen(Atmega328p);
        var sw = new StringWriter();
        codegen.Compile(prog, sw);
        Assert.DoesNotContain("_pgob_", sw.ToString());
    }
}
