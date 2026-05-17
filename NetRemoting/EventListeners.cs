using System.Collections.Concurrent;

namespace NetRemoting;

public class EventListeners
{
    private readonly ConcurrentDictionary<string, List<Delegate>> _listeners = new();

    public void Add(string eventName, Delegate @delegate)
    {
        _listeners
            .GetOrAdd(eventName, x => [])
            .Add(@delegate);
    }

    public void Remove(string eventName, Delegate @delegate)
    {
        _ = _listeners
            .GetOrAdd(eventName, x => [])
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
