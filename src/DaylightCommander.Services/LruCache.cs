namespace DaylightCommander.Services;

/// <summary>용량이 차면 가장 오래 안 쓴 항목부터 버리는 캐시. 스레드 안전. 버려진 값은 화면이 아직 쓸 수 있어 정리하지 않는다 (GC가 거둔다).</summary>
public sealed class LruCache<TKey, TValue>(int capacity) where TKey : notnull
{
    readonly object _gate = new();
    readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map = [];
    readonly LinkedList<(TKey Key, TValue Value)> _order = new(); // 앞이 가장 최근

    public int Count { get { lock (_gate) return _map.Count; } }

    public bool TryGet(TKey key, out TValue value)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
        }
        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                _map.Remove(key);
            }
            _map[key] = _order.AddFirst((key, value));
            while (_map.Count > capacity)
            {
                var last = _order.Last!;
                _order.RemoveLast();
                _map.Remove(last.Value.Key);
                // 화면이 아직 쓰는 비트맵일 수 있어 여기서 Dispose하지 않는다 (GC가 거둔다).
            }
        }
    }

    public void Clear()
    {
        lock (_gate) { _map.Clear(); _order.Clear(); }
    }
}
