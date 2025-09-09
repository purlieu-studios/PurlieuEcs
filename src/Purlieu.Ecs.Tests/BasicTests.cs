using NUnit.Framework;

namespace Purlieu.Ecs.Tests;

[TestFixture]
public class BasicTests
{
    [Test]
    public void API_BasicSetup_ShouldPass()
    {
        // This is a placeholder test to ensure the test framework is working
        Assert.That(true, Is.True);
    }

    [Test]
    public void ALLOC_NoAllocation_ShouldPass()
    {
        // Placeholder allocation test
        var result = 1 + 1;
        Assert.That(result, Is.EqualTo(2));
    }

    [Test]
    public void IT_Integration_ShouldPass()
    {
        // Placeholder integration test
        Assert.That("test", Is.Not.Null);
    }
}