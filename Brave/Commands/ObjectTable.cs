using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Brave.Commands;

public readonly struct ObjectTable(ImmutableArray<object?> constants, object?[] runtime)
{
    private readonly ImmutableArray<object?> _constants = constants;
    private readonly object?[] _runtime = runtime;

    public object? GetConstant(int index) => _constants[index];
    public object? GetRuntime(int index) => _runtime[index];
}