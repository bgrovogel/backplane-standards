using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Fixtures;

/// <summary>
/// The API's contract types never expose a Domain type (<see cref="ContractRule"/>). An application says which folders hold its contracts and which hold its Domain.
/// </summary>
[TestFixture]
public abstract class ContractLayeringConventions : ConventionFixture
{
    /// <summary>Path prefixes, relative to the root with forward slashes, of the folders that hold the contract types: <c>src/MyApp.Application/Contracts/</c>.</summary>
    protected abstract IReadOnlyCollection<string> ContractFolders { get; }

    /// <summary>Path prefixes of the folders that hold the Domain: <c>src/MyApp.Domain/</c>.</summary>
    protected abstract IReadOnlyCollection<string> DomainFolders { get; }

    /// <summary>Guards the rule against a wrong path: the contract folders must hold types.</summary>
    [Test]
    public void ContractTypes_AreFound()
    {
        Assert.That(ContractRule.TypeNames(Production, ContractFolders), Is.Not.Empty, "the contract rule must actually be scanning the folders that hold the contract types");
    }

    /// <summary>Guards the rule against a wrong path: the Domain folders must hold types.</summary>
    [Test]
    public void DomainTypes_AreFound()
    {
        Assert.That(ContractRule.TypeNames(Production, DomainFolders), Is.Not.Empty, "the contract rule must actually be reading the Domain's types to know what a contract may not expose");
    }

    /// <summary>No contract exposes a Domain type.</summary>
    [Test]
    public void ContractTypes_NeverExposeDomainTypes()
    {
        IReadOnlyList<string> leaks = ContractRule.DomainTypesExposed(Production, ContractFolders, DomainFolders);

        Assert.That(leaks, Is.Empty, "a contract uses its own types, so a change to the Domain cannot change the API; declare one in the contracts and map to it:" + Lines(leaks));
    }
}
