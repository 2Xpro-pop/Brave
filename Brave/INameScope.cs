using System;
using System.Collections.Generic;
using System.Text;

namespace Brave;

internal interface INameScope
{
    public object? Find(string name);
}
