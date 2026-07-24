using System.Reflection;
using CConner100.RichEditBoxLite;

namespace RichEditBoxLite.TestApp.Tests;

public class ApiCoverageTests
{
    [Test]
    public void EveryDeclaredDependencyPropertyHasTestUiRegistration()
    {
        var declared = typeof(CConner100.RichEditBoxLite.RichEditBoxLite)
            .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(property => property.Name.EndsWith("Property", StringComparison.Ordinal))
            .Select(property => property.Name[..^"Property".Length])
            .Order()
            .ToArray();

        declared.Should().Equal(CompatibilityCoverage.DependencyProperties.Order());
    }

    [Test]
    public void EveryDeclaredEventHasTestUiRegistration()
    {
        var declared = typeof(CConner100.RichEditBoxLite.RichEditBoxLite)
            .GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(@event => @event.Name)
            .Order()
            .ToArray();

        declared.Should().Equal(CompatibilityCoverage.Events.Order());
    }

    [Test]
    public void ApprovalSnapshotMatchesPublicCompatibilitySurface()
    {
        var members = typeof(CConner100.RichEditBoxLite.RichEditBoxLite).Assembly.ExportedTypes
            .Where(type => type.Namespace == "CConner100.RichEditBoxLite")
            .OrderBy(type => type.FullName)
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(member => member.MemberType is MemberTypes.Constructor or MemberTypes.Event or MemberTypes.Method or MemberTypes.Property)
                .Select(member => $"{type.Name}.{member}"))
            .ToArray();

        members.Should().Contain(value => value.StartsWith("RichEditBoxLite.") && value.Contains("TextChanged"));
        members.Should().Contain(value => value.StartsWith("RichEditTextDocument.") && value.Contains("GetRange"));
        members.Should().Contain(value => value.StartsWith("RichEditTextRange.") && value.Contains("FindText"));
    }
}
