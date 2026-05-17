namespace NetRemoting.Communication;

public class Signature
{
    public string ServiceName { get; set; }

    public Guid? InstanceId { get; set; }

    public string MethodName { get; set; }

    public override bool Equals(object obj)
    {
        return obj is Signature signature &&
               ServiceName == signature.ServiceName &&
               EqualityComparer<Guid?>.Default.Equals(InstanceId, signature.InstanceId) &&
               MethodName == signature.MethodName;
    }

    public override int GetHashCode()
    {
        int hashCode = -1857504455;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ServiceName);
        hashCode = hashCode * -1521134295 + InstanceId.GetHashCode();
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(MethodName);
        return hashCode;
    }
}
