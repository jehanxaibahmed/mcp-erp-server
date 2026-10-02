using System.Reflection;

namespace ErpMcp.UnitTests.Architecture;

/// <summary>
/// Guards the dependency rule: inner layers never reference outer ones.
/// Domain ← Application ← Infrastructure ← Server.
/// </summary>
public class LayeringTests
{
    private static readonly Assembly Domain = typeof(Domain.AssemblyMarker).Assembly;
    private static readonly Assembly Application = typeof(Application.AssemblyMarker).Assembly;

    [Fact]
    public void Domain_references_no_other_project_layer() =>
        ReferencedErpAssemblies(Domain).ShouldBeEmpty();

    [Fact]
    public void Application_references_only_domain() =>
        ReferencedErpAssemblies(Application).ShouldBeSubsetOf(["ErpMcp.Domain"]);

    [Fact]
    public void Application_has_no_infrastructure_packages()
    {
        var forbidden = new[] { "Npgsql", "Dapper", "ModelContextProtocol" };
        Application.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => forbidden.Any(f => name.StartsWith(f, StringComparison.Ordinal)))
            .ShouldBeEmpty();
    }

    private static string[] ReferencedErpAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("ErpMcp.", StringComparison.Ordinal))
            .Order()
            .ToArray();
}
