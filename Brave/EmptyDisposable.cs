using System;
using System.Collections.Generic;
using System.Text;

namespace Brave;

internal sealed class EmptyDisposable: IDisposable
{
    public static readonly IDisposable Instance = new EmptyDisposable();

    private EmptyDisposable() { }

    public void Dispose() { }
}
