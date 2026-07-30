using Xunit;

namespace AMacQConfigEditor.Tests;

public sealed class ProjectSmokeTests
{
    [Fact]
    public void ApplicationAssemblyLoads()
    {
        Assert.NotNull(typeof(App).Assembly);
    }
}
