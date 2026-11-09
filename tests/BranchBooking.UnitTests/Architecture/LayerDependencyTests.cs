using System.Reflection;
using BranchBooking.Api.Controllers;
using BranchBooking.Application;
using BranchBooking.Domain;
using Xunit;

namespace BranchBooking.UnitTests.Architecture;

public class LayerDependencyTests
{
    private const string Prefix = "BranchBooking.";

    private static readonly Assembly Domain = DomainAssembly.Assembly;
    private static readonly Assembly Application = ApplicationAssembly.Assembly;
    private static readonly Assembly Infrastructure = typeof(BranchBooking.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(HealthController).Assembly;

    private static string[] ProjectReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
            .ToArray();

    [Fact]
    public void Domain_references_no_other_BranchBooking_assembly()
    {
        Assert.Empty(ProjectReferences(Domain));
    }

    [Fact]
    public void Application_references_only_Domain_among_BranchBooking_assemblies()
    {
        Assert.All(ProjectReferences(Application), name => Assert.Equal("BranchBooking.Domain", name));
    }

    [Fact]
    public void Infrastructure_does_not_reference_Api_or_Worker()
    {
        var references = ProjectReferences(Infrastructure);

        Assert.DoesNotContain("BranchBooking.Api", references);
        Assert.DoesNotContain("BranchBooking.Worker", references);
    }

    [Fact]
    public void Api_does_not_reference_Worker()
    {
        Assert.DoesNotContain("BranchBooking.Worker", ProjectReferences(Api));
    }
}
