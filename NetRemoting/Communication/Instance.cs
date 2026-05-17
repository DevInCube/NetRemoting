using NetRemoting.Communication;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NetRemoting;

[DebuggerDisplay("{ToString()}")]
public class Instance
{
    public string ServiceName { get; }
    public Guid? InstanceId { get; }

    public Instance()
    {

    }

    public Instance(string serviceName, Guid? instanceId = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException(nameof(serviceName));

        ServiceName = serviceName;
        InstanceId = instanceId;
    }

    public override bool Equals(object obj)
    {
        return obj is Instance instance &&
               ServiceName == instance.ServiceName &&
               EqualityComparer<Guid?>.Default.Equals(InstanceId, instance.InstanceId);
    }

    public override int GetHashCode()
    {
        int hashCode = 1545604566;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ServiceName);
        hashCode = hashCode * -1521134295 + InstanceId.GetHashCode();
        return hashCode;
    }

    public override string ToString()
    {
        return SerializationHelper.FormatInstance(this);
    }
}
