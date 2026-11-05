# Roslyn and conventions tests

> Roslyn is the C# compiler, available as a library. Our **conventions tests** use it to read our own source files and fail the build when a written rule is broken
> (one type per file, names of three letters, `var` only where the type shows). This primer explains how a test can "read code".

**Read time:** 20 min · **Version:** `Microsoft.CodeAnalysis.CSharp` 5.9.0 (`Directory.Packages.props`) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**Roslyn** is Microsoft's C# compiler, written in C# and exposed as a library (`Microsoft.CodeAnalysis`). Instead of only turning code into a program, you can _ask it questions_
about code: "list every method", "where is this variable declared", "is this a `var`?".

A **conventions test** is an ordinary NUnit test that does this to our _own_ source files. It parses every `.cs` file, looks for something that breaks a rule, and fails with a list of
`file:line` locations. The rule is enforced by the build because the test is part of the test run.

## Why we use it

A written style guide is forgotten. Some rules (formatting, unused usings) are covered by the compiler's analyzers ([MSBuild primer](msbuild-and-build-rules.md)), but others are not, or cannot be set
strongly enough to fail a build: "`var` only when the type appears on the right", "one type per file, named after it", "no class nested in another", "every lambda parameter at least three letters",
"every `Assert.That` has a message". A test can express _any_ rule you can describe in code, in a few dozen lines, and the failure message can say exactly how to fix it.

Alternative: write a real Roslyn **analyzer**, which runs inside the compiler and shows squiggles in the editor. That is the better long-term form for some rules, but it is harder to write, package and test.
Conventions tests are quick to write and easy to read; the platform may promote the most-used ones to analyzers later.

## The big idea

> **Code is data.** A compiler turns text into a tree of objects; a test can walk the tree and check anything about its shape.

## How it works inside

1. **Text to tokens.** Roslyn's _lexer_ splits source text into **tokens**: keywords (`var`), identifiers (`loan`), punctuation (`=`), literals (`5`). Spaces and comments are kept as **trivia** attached to tokens, so
   the original text can be rebuilt exactly.
2. **Tokens to a syntax tree.** The _parser_ arranges tokens into a **syntax tree**: nodes for a class, a method, a statement, an expression. Each node has a **kind** (`SyntaxKind.ClassDeclaration`), children, and a position.
   `CSharpSyntaxTree.ParseText(text)` does steps 1 and 2 in one call and needs no project, no references and no compile, so it is fast.

```
 text:   var loan = new Loan(5);

 tree:   LocalDeclarationStatement
           VariableDeclaration
             type: IdentifierName "var"          <-- the keyword the VarConventionTests looks for
             VariableDeclarator "loan"
               EqualsValueClause
                 ObjectCreationExpression "new Loan(5)"   <-- the "right side"; it names the type, so var is allowed
```

3. **Walking.** `tree.GetRoot().DescendantNodes()` yields every node below the root. LINQ filters them: `.OfType<InvocationExpressionSyntax>()` gives every method call; pattern matching (`node is ClassDeclarationSyntax { ... }`) tests
   shape.
4. **Locations.** `node.GetLocation().GetLineSpan()` returns the file path and line, so a failure can say `src/X.cs:42`.
5. **Two levels of knowledge.** The **syntax tree** knows only what is written. It does _not_ know that `loan` is of type `Loan`, or that `Foo()` is a method of another class. The **semantic model** (built from a
   `CSharpCompilation` that includes referenced assemblies) does know. Semantic analysis is slower and needs the project's references. Our conventions tests use the **syntax tree only**, which is why the `var` rule is phrased as
   "the right side _visibly_ names the type" instead of "the type is obvious".
6. **Scanning the right files.** The tests find the repository root from their own source location (the compile-time path, not the output folder), enumerate `*.cs` under `src` and `tests`, and skip `bin`, `obj` and
   generated migrations. The parsed trees are cached in a `Lazy` so every test in the run shares one parse.
7. **Proving the scanner works.** Each file has a test like `SourceFiles_AreFound` asserting that more than 100 files were scanned. Without it, a wrong path would scan nothing and every rule would pass forever.

For non-C# files the tests use plain **regular expressions** (the Playwright rules read TypeScript text) because there is no Roslyn for TypeScript; the principle is the same.

## How it fits with everything else

- They are **NUnit tests** ([primer](nunit.md)), run in the same `dotnet test`, so G-08 is part of tier 2.
- They complement the **analyzers and `.editorconfig`** ([MSBuild primer](msbuild-and-build-rules.md)): analyzers for what the compiler can express, tests for the rest.
- **Architecture tests** use a different technique (assembly references, [NetArchTest primer](netarchtest-and-ratchets.md)); together they cover "how is the code written" and "what depends on what".
- **ESLint** does the same job for TypeScript ([primer](eslint-prettier-stylelint.md)), including the same three-letter-name rule.
- In the platform they ship as a **NuGet package** with a tiny runner project per application that points them at that application's folders ([Strategy](../00-strategy.md#what-we-standardize)).

## An example from our code

`tests/MortgageAnalytics.Conventions.Tests/VarConventionTests.cs` enforces the `var` rule. The heart of it:

```csharp
List<string> violations = Trees.Value
    .SelectMany(tree => tree.GetRoot().DescendantNodes())   // every node in every file
    .Select(VarUse)                                          // is this a declaration written with var? (with its right side)
    .OfType<(SyntaxToken Keyword, ExpressionSyntax? Value)>()
    .Where(use => !NamesItsType(use.Value))                  // does the right side name the type?
    .Select(use => $"{Location(use.Keyword)}  {FirstLine(use.Keyword.Parent!.Parent!)}")
    .Distinct()
    .ToList();

Assert.That(violations, Is.Empty, "write the type out (decimal interest = ..., not var interest = ...); ...\n" + string.Join('\n', violations));
```

And the rule itself:

```csharp
private static bool NamesItsType(ExpressionSyntax? value) => value switch
{
    null => false,
    ObjectCreationExpressionSyntax or ArrayCreationExpressionSyntax or CastExpressionSyntax or DefaultExpressionSyntax => true,
    BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression) => true,
    AwaitExpressionSyntax await => NamesItsType(await.Expression),
    _ => value.DescendantNodesAndSelf().Any(node => node is AnonymousObjectCreationExpressionSyntax),
};
```

Read it as: `var` is fine if the right side is a `new ...`, an array creation, a cast, an `as`, a `default(T)`, an `await` of one of those, or anything containing an anonymous object (which has no
name to write). Anything else must spell the type, so `decimal interest = LoanTerms.MonthlyInterestOn(balance, rate)` tells the reader it is money without opening the method.

When the rule was introduced the test was **red on 2,210 declarations** (575 in `src`, 1,635 in `tests`); they were rewritten, and from then on the test keeps the count at zero.

The sibling tests: `NamingConventionTests` (names of three letters), `FileLayoutConventionTests` (one type per file, no nested classes), `AssertionConventionTests` (messages, `Assert.Multiple`),
`EndToEndConventionTests` (Playwright `expect` messages, regex-based).

## How to use it

To add a rule:

1. **Write the rule in one sentence** and decide its exceptions.
2. **Add the scanner-alive test** first (it asserts files were found).
3. **Write the rule as a test** that collects violations as `path:line  text` strings and asserts the list is empty, with a message that says how to fix.
4. **Run it; expect red** on existing code. Fix the code (a tool such as a Roslyn-based rewriter helps for hundreds of cases) until green. If the count is too large to fix at once, use a **ratchet** instead
   ([primer](netarchtest-and-ratchets.md)).
5. **Document it** in the testing page and the conventions count in the docs (the docs check verifies the count).

Handy Roslyn queries: `DescendantNodes().OfType<ClassDeclarationSyntax>()`, `node.Identifier.Text`, `node.Modifiers`, `node.Parent`, `SyntaxKind` checks, `node.GetLocation().GetLineSpan().StartLinePosition.Line`.

## The rules we follow

| Rule                                                               | Enforced by                            |
| ------------------------------------------------------------------ | -------------------------------------- |
| Each conventions test scans real files and asserts it found them   | `SourceFiles_AreFound` in each fixture |
| A violation message gives `path:line` and how to fix               | Review                                 |
| Tests parse the syntax only unless a rule truly needs meaning      | Review                                 |
| A new rule arrives with the code already fixed, or as a ratchet    | Review                                 |
| Generated code (migrations, `bin`, `obj`) is excluded in one place | `TestSourceFiles`                      |

## What goes wrong

| Symptom                                                   | Cause                                                         | Fix                                                      |
| --------------------------------------------------------- | ------------------------------------------------------------- | -------------------------------------------------------- |
| A rule passes everywhere, even where it is clearly broken | The scanner found no files (wrong path), or filters skip them | The scanner-alive test; print the count                  |
| A false positive on valid code                            | The rule needs meaning the syntax tree lacks                  | Use the semantic model, or narrow the rule               |
| The test is slow                                          | Re-parsing files in every test                                | Parse once in a `Lazy` shared by the fixture             |
| A new rule is red on thousands of lines                   | Adopted without a plan                                        | A ratchet that only tightens, or an automated rewrite    |
| Different results on CI than locally                      | Path separators or line endings                               | Normalize `\` and `/`; compare with `ReplaceLineEndings` |
| A regex rule breaks on a comma inside a call              | Regex is not a parser                                         | Put the value in a variable first, or use a real parser  |

## Terms

| Term               | Meaning                                                              |
| ------------------ | -------------------------------------------------------------------- |
| **Compiler**       | A program that turns source code into something runnable             |
| **Token**          | The smallest piece of source: a keyword, a name, a symbol            |
| **Trivia**         | Spaces, line breaks and comments between tokens                      |
| **Syntax tree**    | The structure of the code as nested nodes                            |
| **Node**           | One element of the tree (a class, a method, an expression)           |
| **Semantic model** | The compiler's knowledge of meaning: types, symbols, references      |
| **Analyzer**       | A rule library that runs inside the compiler during a build          |
| **Ratchet**        | A test that allows a number of violations and only ever lets it fall |

## As a package

Since 2026-11-04 the rules are a package, `Platform.Conventions` (roadmap task 1.3), so an application does not copy them. Each rule is a plain class that takes a `SourceSet` and returns violations; each has an abstract NUnit fixture
that an application inherits in one line (`public class Var : VarConventions { }`), with one `[assembly: Conventions]` attribute saying where the repository is. A setting that differs per application is a virtual property.
The package was lifted out of `mortgage-analytics` and checked by running it on that application: every rule comes back clean and the methods past a size limit are exactly the ones its ratchet lists. Running
the package's fixtures on the package's own code found real violations at once (a two-letter method name, three classes in one file, a method too complex), which were fixed.
See [the package's README](../../src/Platform.Conventions/README.md).

## Further reading

- [Roslyn overview](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/)
- [Get started with syntax analysis](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/get-started/syntax-analysis)
- [Syntax Visualizer](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/syntax-visualizer): see the tree for any code in Visual Studio
