using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class SolutionSmokeTests
{
    [Fact]
    public void Core_assembly_name_matches_product()
    {
        var name = typeof(AssemblyMarker).Assembly.GetName().Name;
        Assert.Equal(AssemblyMarker.Name, name);
    }
}
