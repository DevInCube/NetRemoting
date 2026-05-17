using NetRemoting.Test.Interfaces;

namespace NetRemoting.Test.Implementations;

public class Dependency : IDependency
{
    public void SetMe(string me)
    {
        Console.WriteLine($"SET ME: {me}");
    }
}
