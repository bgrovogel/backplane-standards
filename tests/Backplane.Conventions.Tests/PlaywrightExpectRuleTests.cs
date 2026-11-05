using Backplane.Conventions.Rules;

namespace Backplane.Conventions.Tests;

/// <summary>The Playwright expect rule, proved on calls that break it and calls that do not.</summary>
[TestFixture]
public class PlaywrightExpectRuleTests
{
    private static IReadOnlyList<string> Found(string code) => PlaywrightExpectRule.WithoutAMessage(TextSet.OfText(("e2e/a.spec.ts", code)));

    [TestCase("expect(page).toHaveURL('/x');", TestName = "A matcher with no message")]
    [TestCase("expect(page.locator('h1')).not.toBeVisible();", TestName = "A negated matcher with no message")]
    [TestCase("expect(count(first, 'a')).toBe(2);", TestName = "A comma inside a nested call is not a message")]
    [TestCase("expect([1, 'a']).toHaveLength(2);", TestName = "A comma inside an array is not a message")]
    [TestCase("expect({ a: 1, b: 'x' }).toEqual(value);", TestName = "A comma inside an object is not a message")]
    [TestCase("expect('x, \"y\"').toBe(1);", TestName = "A comma and quotes inside the first argument's string")]
    [TestCase("expect(')').toBe(1);", TestName = "A closing bracket inside a string")]
    [TestCase("expect('it\\'s').toBe(1);", TestName = "An escaped quote inside the first argument")]
    [TestCase("expect(value, 5).toBe(1);", TestName = "A second argument that is not text")]
    [TestCase("expect(value, message).toBe(1);", TestName = "A second argument that is a variable")]
    [TestCase("expect(\n  page.locator('x'),\n).toBeVisible();", TestName = "Over several lines")]
    [TestCase("expect(page)  .  toHaveTitle('x');", TestName = "With spaces around the dot")]
    public void AnExpectWithoutAMessage_IsFound(string code)
    {
        Assert.That(Found(code), Has.Count.EqualTo(1), "a failure should say what it means");
    }

    [TestCase("expect(page, 'the page is the home page').toHaveURL('/x');", TestName = "A single-quoted message")]
    [TestCase("expect(page, \"the page\").toHaveURL('/x');", TestName = "A double-quoted message")]
    [TestCase("expect(page, `the page of ${name}`).toHaveURL('/x');", TestName = "A template message")]
    [TestCase("expect(page.locator('h1'), 'the heading is hidden').not.toBeVisible();", TestName = "A negated matcher with a message")]
    [TestCase("expect(count(first, 'a'), 'two of them').toBe(2);", TestName = "A nested call and a message")]
    [TestCase("expect(value,\n  'a message on its own line',\n).toBe(1);", TestName = "The message on its own line")]
    [TestCase("expect(value, 'a message, with a comma (and a paren').toBe(1);", TestName = "A message holding commas and a bracket")]
    [TestCase("expect(value, 'it\\'s fine').toBe(1);", TestName = "A message holding an escaped quote")]
    public void AnExpectWithAMessage_IsFine(string code)
    {
        Assert.That(Found(code), Is.Empty, "the message explains the failure");
    }

    [TestCase("const expectation = 1; expectation.toBe(2);", TestName = "A word that merely contains expect")]
    [TestCase("// expect(value) with no matcher yet", TestName = "A call with no matcher after it")]
    [TestCase("expect.soft(page, 'soft').toHaveURL('/x');", TestName = "The soft form")]
    [TestCase("const unfinished = expect(page", TestName = "A call that is never closed")]
    [TestCase(".toBe(1); expect(unclosed", TestName = "A matcher before a call that is never closed")]
    [TestCase("AB.toBe(1); expect(unclosed", TestName = "A matcher two characters in, before a call that is never closed")]
    [TestCase("expect('never closed", TestName = "A string that is never closed")]
    public void WhatIsNotAnExpectCallWithAMatcher_IsLeftAlone(string code)
    {
        Assert.That(Found(code), Is.Empty, "only expect(...).toX(...) is a Playwright assertion");
    }

    [Test]
    public void EveryCallInAFileIsJudged_AndAViolationNamesItsFileLineAndFirstLine()
    {
        const string code = "import { expect } from '@playwright/test';\n\ntest('x', async ({ page }) => {\n  expect(page, 'ok').toHaveURL('/');\n  expect(page.locator('a'),\n  ).toBeVisible();\n  expect(page).toHaveTitle('t');\n});\n";

        IReadOnlyList<string> found = Found(code);

        Assert.That(found, Is.EqualTo(new[] { "e2e/a.spec.ts:5  expect(page.locator('a'),", "e2e/a.spec.ts:7  expect(page).toHaveTitle" }), "a path and line, then the call as far as the matcher, first line only");
    }

    [Test]
    public void TheCallCount_CountsEveryExpectWithAMatcher_WithOrWithoutAMessage()
    {
        TextSet set = TextSet.OfText(("e2e/a.spec.ts", "expect(a).toBe(1);\nexpect(b, 'fine').not.toBe(2);\nexpect(c);\n"), ("e2e/b.spec.ts", "expect(d, 'x').toBeVisible();"));

        Assert.That(PlaywrightExpectRule.CallCount(set), Is.EqualTo(3), "a scan that saw none would pass without having looked");
    }

    [Test]
    public void SeveralFiles_AreAllRead()
    {
        TextSet set = TextSet.OfText(("e2e/a.spec.ts", "expect(a).toBe(1);"), ("e2e/b.spec.ts", "expect(b, 'fine').toBe(1);"), ("e2e/c.spec.ts", "expect(c).toBe(1);"));

        Assert.That(PlaywrightExpectRule.WithoutAMessage(set).Select(line => line.Split(':')[0]), Is.EqualTo(new[] { "e2e/a.spec.ts", "e2e/c.spec.ts" }), "each file is judged on its own");
    }
}
