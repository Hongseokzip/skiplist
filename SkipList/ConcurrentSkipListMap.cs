﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace SkipList
{
    // todo: support concurrency
    // todo: implement IDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>>, IEnumerable<KeyValuePair<TKey, TValue>, IEnumerable
    public class ConcurrentSkipListMap : IEnumerable<KeyValuePair<Int32, Int32>>
    {
        public static readonly Int32 MAX_FORWARD_LENGTH = 20;
        private readonly Double _p;
        private readonly Random _random;
        private readonly ConcurrentSkipListMapHeadNode _head;

        public ConcurrentSkipListMap(Double p = 0.5)
        {
            this._p = p;
            _random = new Random(0x0d0ffFED);
            _head = new ConcurrentSkipListMapHeadNode(MAX_FORWARD_LENGTH);
        }

        private Int32 _count = 0;
        public Int32 Count
        {
            get
            {
                Debug.Assert(0 <= _count);
                return _count;
            }
        }

        public Boolean IsReadOnly => false;

        public Boolean TryGetValue(Int32 key, out Int32 value)
        {
            ConcurrentSkipListMapNode traverseNode = _head;
            var nextIndex = TraverseNextStep(traverseNode.Forwards, key);
            while (nextIndex != null)
            {
                traverseNode = traverseNode.Forwards[nextIndex.Value];

                if (traverseNode.Key == key)
                {
                    value = traverseNode.Value;
                    return true;
                }

                nextIndex = TraverseNextStep(traverseNode.Forwards, key);
            }

            value = 0;
            return false;
        }

        public void Add(Int32 key, Int32 value)
        {
            ConcurrentSkipListMapNode traverseNode = _head;
            var backlook = GenerateInitialBacklook();
            var nextIndex = TraverseNextStep(_head.Forwards, key);

            while (nextIndex != null)
            {
                for (var i = nextIndex.Value; i < traverseNode.Forwards.Length; i++)
                {
                    backlook[i] = traverseNode;
                }

                traverseNode = traverseNode.Forwards[nextIndex.Value];

                if (traverseNode.Key == key)
                {
                    throw new ArgumentException("the key already exists", nameof(key));
                }

                nextIndex = TraverseNextStep(traverseNode.Forwards, key);
            }

            for (var i = 0; i < traverseNode.Forwards.Length; i++)
            {
                backlook[i] = traverseNode;
            }

            var forwardLength = NewForwardLength();
            var newNode = new ConcurrentSkipListMapNode(forwardLength) { Key = key, Value = value };
            for (var i = 0; i < forwardLength; i++)
            {
                var prevNode = backlook[i];
                var nextNode = prevNode?.Forwards[i];

                newNode.Forwards[i] = nextNode;
                prevNode.Forwards[i] = newNode;
            }

            _count++;
        }

        public bool Remove(Int32 key)
        {
            if (_count == 0)
            {
                return false;
            }

            ConcurrentSkipListMapNode traverseNode = _head;
            var backlook = GenerateInitialBacklook();
            var nextIndex = TraverseNextStep(traverseNode.Forwards, key);
            Boolean found = false;

            while (nextIndex != null)
            {
                for (var i = nextIndex.Value; i < traverseNode.Forwards.Length; i++)
                {
                    backlook[i] = traverseNode;
                }

                traverseNode = traverseNode.Forwards[nextIndex.Value];

                var traverseNodeKey = (traverseNode as ConcurrentSkipListMapNode).Key;
                if (traverseNodeKey == key)
                {
                    found = true;
                    break;
                }
                else if (key < traverseNodeKey)
                {
                    return false;
                }

                nextIndex = TraverseNextStep(traverseNode.Forwards, key);
            }

            if (found == false)
            {
                return false;
            }

            var foundNode = traverseNode;
            var prevNode = backlook[nextIndex.Value];

            for (var i = 0; i < nextIndex.Value; i++)
            {
                backlook[i] = prevNode;
            }

            for (var i = 0; i < foundNode.Forwards.Length; i++)
            {
                backlook[i].Forwards[i] = foundNode.Forwards[i];
            }

            _count--;
            return true;
        }

        public bool ContainsKey(Int32 key)
        {
            return TryGetValue(key, out var value);
        }

        private Int32? TraverseNextStep(ConcurrentSkipListMapNode[] forwards, Int32 targetKey)
        {
            if (forwards == null)
            {
                throw new ArgumentNullException();
            }

            for (var i = forwards.Length - 1; 0 <= i; i--)
            {
                if (forwards[i]?.Key <= targetKey)
                {
                    return i;
                }
            }

            return null;
        }

        private Int32 NewForwardLength()
        {
            var r = _random.NextDouble();

            for (var length = 1; length <= MAX_FORWARD_LENGTH; length++)
            {
                if (Math.Pow(_p, length) < r)
                {
                    return length;
                }
            }

            return MAX_FORWARD_LENGTH;
        }

        private ConcurrentSkipListMapNode[] GenerateInitialBacklook()
        {
            var backlook = new ConcurrentSkipListMapNode[_head.Forwards.Length];
            for (var i = 0; i < backlook.Length; i++)
            {
                backlook[i] = _head;
            }

            return backlook;
        }

        #region Enumerator
        public IEnumerator<KeyValuePair<int, int>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        public struct Enumerator : IEnumerator<KeyValuePair<Int32, Int32>>, IEnumerator
        {
            private readonly ConcurrentSkipListMap _skipListMap;
            private ConcurrentSkipListMapNode _currentNode;
            private KeyValuePair<Int32, Int32> _current;

            internal Enumerator(ConcurrentSkipListMap skipListMap)
            {
                _skipListMap = skipListMap;
                _currentNode = _skipListMap._head;
                _current = new KeyValuePair<int, int>();
            }

            public bool MoveNext()
            {
                if (_currentNode.Forwards[0] != null)
                {
                    _currentNode = _currentNode.Forwards[0];
                    _current = new KeyValuePair<int, int>(_currentNode.Key, _currentNode.Value);
                    return true;
                }

                _currentNode = default;
                _current = new KeyValuePair<int, int>();
                return false;
            }

            public void Reset()
            {
                _currentNode = _skipListMap._head;
                _current = new KeyValuePair<int, int>();
            }

            public KeyValuePair<int, int> Current => _current;

            object IEnumerator.Current => _current;

            public void Dispose() { }
        }
        #endregion
    }
}
