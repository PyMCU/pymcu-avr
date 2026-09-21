// SPDX-License-Identifier: MIT
using System.Text.Json.Serialization;

namespace PyMCU.Backend.Targets.AVR;

/// <summary>
/// One IR-level block boundary: an entry is a function (or outlined subroutine)
/// entry point; a non-entry is a MIR <c>Label</c> node. <c>WordAddr</c> stays null
/// in the backend's own output: the assembler/linker (-mrelax) is free to shrink
/// CALL/JMP after codegen, so final addresses are filled in by the driver from the
/// linked ELF symtab before the map reaches the profiler.
/// </summary>
public record BlockMapEntry(string Function, string Label, bool Entry, uint? WordAddr);

/// <summary>
/// One IR conditional jump. <c>Sym</c> is the <c>_pgob_N</c> marker label emitted
/// at the head of the jump's instruction sequence, so the post-link resolver can
/// read its real address from the ELF symtab. <c>Taken</c>/<c>Fallthrough</c> are
/// MIR labels verbatim; <c>Fallthrough</c> is null when the jump ends a function.
/// </summary>
public record BlockMapBranch(int Id, string Function, string Sym,
    string Taken, string? Fallthrough, uint? WordAddr);

public record BlockMap(int Format, List<BlockMapEntry> Blocks, List<BlockMapBranch> Branches);

[JsonSerializable(typeof(BlockMap))]
internal partial class AvrBlockMapJsonContext : JsonSerializerContext { }
