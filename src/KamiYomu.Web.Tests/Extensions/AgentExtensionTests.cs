using System.Reflection;

using KamiYomu.Web.Extensions;

namespace KamiYomu.Web.Tests.Extensions;

public class AgentExtensionTests
{
    [Fact]
    public void FindImplementations_ThrowsWhenProvidedTypeIsNotAnInterface()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            AgentExtension.FindImplementations(Assembly.GetExecutingAssembly(), typeof(ConcreteAgent)).ToList());

        Assert.Equal("interfaceType", exception.ParamName);
    }

    [Fact]
    public void FindImplementations_ReturnsConcreteImplementationsOfTheInterface()
    {
        List<Type> result = AgentExtension.FindImplementations(
            Assembly.GetExecutingAssembly(),
            typeof(ITestAgent))
            .ToList();

        Assert.Contains(typeof(ConcreteAgent), result);
        Assert.Contains(typeof(AnotherConcreteAgent), result);
        Assert.DoesNotContain(typeof(AbstractAgent), result);
        Assert.DoesNotContain(typeof(UnrelatedType), result);
    }

    public interface ITestAgent
    {
    }

    public abstract class AbstractAgent : ITestAgent
    {
    }

    public class ConcreteAgent : AbstractAgent
    {
    }

    public class AnotherConcreteAgent : ITestAgent
    {
    }

    public class UnrelatedType
    {
    }
}
