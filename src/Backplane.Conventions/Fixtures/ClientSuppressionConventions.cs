using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Fixtures;

/// <summary>The client's suppression standard (<see cref="ClientSuppressionRule"/>) over its TypeScript and templates.</summary>
[TestFixture]
public abstract class ClientSuppressionConventions : ConventionFixture
{
    /// <summary>Guards the rule against a wrong path.</summary>
    [Test]
    public void ClientFiles_AreFound()
    {
        Assert.That(Client.Files, Is.Not.Empty, "the client suppression rule must actually be scanning the application's client");
    }

    /// <summary>Every suppression says why, and the two that hide too much are not used.</summary>
    [Test]
    public void NoClientSuppression_IsLeftUnexplained()
    {
        IReadOnlyList<string> violations = ClientSuppressionRule.Violations(Client);

        Assert.That(violations, Is.Empty, "write `-- reason` after eslint-disable, say what @ts-expect-error expects, and fix instead of @ts-ignore:" + Lines(violations));
    }
}
