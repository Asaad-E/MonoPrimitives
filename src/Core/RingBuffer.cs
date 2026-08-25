using System;
using System.Collections;
using System.Collections.Generic;

namespace MonoPrimitives
{
    /// <summary>A fixed-capacity ring buffer — oldest entries are overwritten once full. <c>foreach</c> allocates nothing.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    public sealed class RingBuffer<T> : IEnumerable<T>
    {
        private readonly T[] _items;
        private int _newestIndex = -1;

        /// <summary>Maximum number of elements this buffer holds.</summary>
        public int Capacity => _items.Length;

        /// <summary>How many elements are actually recorded so far (grows to <see cref="Capacity"/>, then stays there).</summary>
        public int Count { get; private set; }

        /// <summary>The most recently added element. Throws <see cref="InvalidOperationException"/> if the buffer is empty.</summary>
        public T Newest => Count > 0 ? this[Count - 1] : throw new InvalidOperationException("RingBuffer is empty.");

        /// <summary>The oldest element still recorded. Throws <see cref="InvalidOperationException"/> if the buffer is empty.</summary>
        public T Oldest => Count > 0 ? this[0] : throw new InvalidOperationException("RingBuffer is empty.");

        /// <summary>Creates an empty buffer holding up to <paramref name="capacity"/> elements.</summary>
        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "capacity must be positive.");
            _items = new T[capacity];
        }

        /// <summary>Appends <paramref name="item"/>, evicting the oldest element once <see cref="Capacity"/> is reached.</summary>
        public void Add(T item)
        {
            _newestIndex = (_newestIndex + 1) % _items.Length;
            _items[_newestIndex] = item;
            if (Count < _items.Length) Count++;
        }

        /// <summary>Drops every recorded element and clears the backing slots (so a <typeparamref name="T"/> that's a reference type doesn't keep the GC from collecting what it pointed to).</summary>
        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            Count = 0;
            _newestIndex = -1;
        }

        /// <summary>Element at <paramref name="indexFromOldest"/> — <c>0</c> is the oldest recorded element, <see cref="Count"/><c>-1</c> is the newest.</summary>
        public T this[int indexFromOldest]
        {
            get
            {
                if (indexFromOldest < 0 || indexFromOldest >= Count)
                    throw new ArgumentOutOfRangeException(nameof(indexFromOldest));
                int oldestIndex = (_newestIndex - Count + 1 + _items.Length) % _items.Length;
                return _items[(oldestIndex + indexFromOldest) % _items.Length];
            }
        }

        /// <summary>Enumerates elements oldest-first to newest-last, matching the indexer's own order. Allocation-free when used as <c>foreach (var x in ringBuffer)</c> against this concrete type.</summary>
        public Enumerator GetEnumerator() => new(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Allocation-free enumerator for <see cref="RingBuffer{T}"/> — a struct, so a plain <c>foreach</c> against this concrete type never boxes it.</summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly RingBuffer<T> _buffer;
            private int _index;

            internal Enumerator(RingBuffer<T> buffer)
            {
                _buffer = buffer;
                _index = -1;
            }

            /// <summary>The element at the enumerator's current position.</summary>
            public readonly T Current => _buffer[_index];

            readonly object? IEnumerator.Current => Current;

            /// <summary>Advances to the next element. False once past the last one.</summary>
            public bool MoveNext()
            {
                _index++;
                return _index < _buffer.Count;
            }

            /// <summary>Resets to before the first element.</summary>
            public void Reset() => _index = -1;

            /// <summary>No unmanaged resources to release — present only to satisfy <see cref="IEnumerator"/>.</summary>
            public readonly void Dispose() { }
        }
    }
}
