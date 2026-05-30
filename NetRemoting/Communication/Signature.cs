namespace NetRemoting.Communication;

public class Signature
{
    public required string ServiceName { get; set; }

    public Guid? InstanceId { get; set; }

    public required string MethodName { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is Signature signature &&
               ServiceName == signature.ServiceName &&
               EqualityComparer<Guid?>.Default.Equals(InstanceId, signature.InstanceId) &&
               MethodName == signature.MethodName;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ServiceName, InstanceId, MethodName);
    }
}
