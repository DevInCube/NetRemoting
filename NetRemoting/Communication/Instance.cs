using NetRemoting.Communication;
using System.Diagnostics;

namespace NetRemoting;

[DebuggerDisplay("{ToString()}")]
public class Instance
{
    public string ServiceName { get; }

    public Guid? InstanceId { get; }

    public Instance(string serviceName, Guid? instanceId = null)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(serviceName);

        ServiceName = serviceName;
        InstanceId = instanceId;
    }

    public override bool Equals(object? obj)
    {
        return obj is Instance instance &&
               ServiceName == instance.ServiceName &&
               EqualityComparer<Guid?>.Default.Equals(InstanceId, instance.InstanceId);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ServiceName, InstanceId);
    }

    public override string ToString()
    {
        return DisplayFormatter.FormatInstance(this);
    }
}
