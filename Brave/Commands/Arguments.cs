using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Brave.Commands;

public readonly struct Arguments : IReadOnlyList<object>
{
    public static readonly Arguments Empty = new(null);

    private readonly object? _args;

    public Arguments(object? args)
    {
        _args = args;
    }

    public int Count
    {
        get
        {
            if (_args == null)
            {
                return 0;
            }

            if(_args is System.Collections.ICollection collection)
            {
                return collection.Count;
            }

            return 1;
        }
    }

    public object this[int index]
    {
        get
        {
            if (_args == null)
            {
                throw new IndexOutOfRangeException();
            }
            
            if(_args is System.Collections.IList list)
            {
                return list[index]!;
            }

            if(index == 0)
            {
                return _args;
            }

            throw new IndexOutOfRangeException();
        }
    }

    public ArgumentsEnumerator GetEnumerator() => new(this);

    IEnumerator<object> IEnumerable<object>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public struct ArgumentsEnumerator : IEnumerator<object>
    {
        private readonly Arguments _arguments;
        private int _index;
        public ArgumentsEnumerator(Arguments arguments)
        {
            _arguments = arguments;
            _index = -1;
        }
        public readonly object Current => _arguments[_index];
        readonly object IEnumerator.Current => Current;
        public readonly void Dispose()
        {
        }
        public bool MoveNext()
        {
            _index++;
            return _index < _arguments.Count;
        }
        public void Reset()
        {
            _index = -1;
        }
    }
}
