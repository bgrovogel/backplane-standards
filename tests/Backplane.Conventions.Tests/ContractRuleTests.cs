using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The contract-layering rule, proved on samples that break it and samples that do not.</summary>
[TestFixture]
public class ContractRuleTests
{
    private const string DomainCode = "namespace App.Domain; public readonly record struct Money(decimal Value); public enum Kind { Fixed } public interface IRule { } public sealed class Loan { } public struct Rate { } public delegate void Done();";

    private static readonly string[] Contracts = ["src/App.Application/Contracts/"];
    private static readonly string[] Domain = ["src/App.Domain/"];

    [Test]
    public void TheDomainTypes_AreEveryKindOfTypeDeclaredInTheDomainFolder_AndNothingElse()
    {
        SourceSet sample = SourceSet.OfText(("src/App.Domain/Types.cs", DomainCode), ("src/App.Application/Other.cs", "class Elsewhere { }"));

        Assert.That(ContractRule.TypeNames(sample, Domain), Is.EquivalentTo(new[] { "Money", "Kind", "IRule", "Loan", "Rate", "Done" }), "a record, enum, interface, class, struct and delegate in the domain, and no type from another folder");
    }

    [Test]
    public void APublicPropertyOfADomainType_IsFound_HoweverTheTypeIsWrapped()
    {
        SourceSet sample = SourceSet.OfText(
            ("src/App.Domain/Types.cs", DomainCode),
            ("src/App.Application/Contracts/LoanDto.cs", "public sealed class LoanDto { public Money Plain { get; set; } public List<Money> Many { get; set; } public Kind? Maybe { get; set; } public Money[] Array { get; set; } public App.Domain.Loan Qualified { get; set; } }"));

        IReadOnlyList<string> leaks = ContractRule.DomainTypesExposed(sample, Contracts, Domain);

        Assert.That(
            leaks,
            Is.EqualTo(new[]
            {
                "src/App.Application/Contracts/LoanDto.cs:1  LoanDto.Plain exposes the domain type Money",
                "src/App.Application/Contracts/LoanDto.cs:1  LoanDto.Many exposes the domain type Money",
                "src/App.Application/Contracts/LoanDto.cs:1  LoanDto.Maybe exposes the domain type Kind",
                "src/App.Application/Contracts/LoanDto.cs:1  LoanDto.Array exposes the domain type Money",
                "src/App.Application/Contracts/LoanDto.cs:1  LoanDto.Qualified exposes the domain type Loan",
            }),
            "a plain type, a generic argument, a nullable, an array element and a qualified name all expose it");
    }

    [Test]
    public void APositionalRecordParameter_IsAPublicProperty_AndIsFound()
    {
        SourceSet sample = SourceSet.OfText(("src/App.Domain/Types.cs", DomainCode), ("src/App.Application/Contracts/Request.cs", "public sealed record Request(Money Amount, decimal Rate);"));

        Assert.That(ContractRule.DomainTypesExposed(sample, Contracts, Domain), Is.EqualTo(new[] { "src/App.Application/Contracts/Request.cs:1  Request.Amount exposes the domain type Money" }), "the parameter becomes a property; Rate is a decimal");
    }

    [Test]
    public void WhatIsNotPartOfTheContract_IsNotFound()
    {
        SourceSet sample = SourceSet.OfText(
            ("src/App.Domain/Types.cs", DomainCode),
            ("src/App.Application/Contracts/Quiet.cs", "public sealed class Quiet { private Money Hidden { get; set; } internal Money Inside { get; set; } public static Money Shared { get; set; } public decimal Fine { get; set; } public Money Method() => default; }"),
            ("src/App.Application/Mapper.cs", "public sealed class Mapper { public Money Maps { get; set; } }"),
            ("src/App.Api/Endpoint.cs", "public sealed class Endpoint { public Loan Shown { get; set; } }"));

        Assert.That(ContractRule.DomainTypesExposed(sample, Contracts, Domain), Is.Empty, "private, internal and static members, methods, and types outside the contracts folder are not the API's shape");
    }

    [Test]
    public void AContractTypeWithTheSameNameAsADomainType_IsTheContractsOwn_AndIsNotFound()
    {
        SourceSet sample = SourceSet.OfText(
            ("src/App.Domain/Types.cs", DomainCode),
            ("src/App.Application/Contracts/Money.cs", "public sealed record Money(decimal Value);"),
            ("src/App.Application/Contracts/Request.cs", "public sealed record Request(Money Amount);"));

        Assert.That(ContractRule.DomainTypesExposed(sample, Contracts, Domain), Is.Empty, "a name the contracts declare themselves means the contract's own type, mapped from the domain's");
    }
}
