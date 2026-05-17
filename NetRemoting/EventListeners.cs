using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NetRemoting;

public class EventListeners
{
    private readonly ConcurrentDictionary<string, List<Delegate>> _listeners = new ConcurrentDictionary<string, List<Delegate>>();

    public void Add(string eventName, Delegate @delegate)
    {
        _listeners
            .GetOrAdd(eventName, x => new List<Delegate>())
            .Add(@delegate);
    }

    public void Remove(string eventName, Delegate @delegate)
    {
        _ = _listeners
            .GetOrAdd(eventName, x => new List<Delegate>())
            .Remove(@delegate);
    }

    public IList<Delegate> GetInvocationList(string eventName)
    {
        if (!_listeners.TryGetValue(eventName, out var eventListeners) ||
            eventListeners.Count == 0)
        {
            throw new ArgumentNullException(nameof(eventName));
        }

        return eventListeners;
    }
}
