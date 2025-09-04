using System;
using System.Collections.Generic;

namespace Schema.Core.DataStructures
{
    public class LRUCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _cacheMap;
        private readonly LinkedList<(TKey Key, TValue Value)> _lruList;
    
        public LRUCache(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than zero.");
    
            _capacity = capacity;
            _cacheMap = new Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>>();
            _lruList = new LinkedList<(TKey Key, TValue Value)>();
        }
    
        public bool TryGet(TKey key, out TValue value)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                // Move the accessed node to the front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
    
            value = default;
            return false;
        }
    
        public void Put(TKey key, TValue value)
        {
            if (_cacheMap.TryGetValue(key, out var existingNode))
            {
                // Update the value and move to the front
                _lruList.Remove(existingNode);
                _cacheMap[key] = _lruList.AddFirst((key, value));
            }
            else
            {
                if (_cacheMap.Count >= _capacity)
                {
                    // Remove the least recently used item
                    var lru = _lruList.Last;
                    if (lru != null)
                    {
                        _cacheMap.Remove(lru.Value.Key);
                        _lruList.RemoveLast();
                    }
                }
    
                // Add the new item to the cache
                _cacheMap[key] = _lruList.AddFirst((key, value));
            }
        }
    }
}