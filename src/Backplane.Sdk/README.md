# Backplane.Sdk

The build rules every application starts with, as an MSBuild SDK package. It holds no code: `Sdk/Sdk.props` sets the defaults before restore (`net10.0`, nullable, warnings as errors,
`latest-recommended` analysis, code style enforced in the build, a lock file, an XML documentation file), `Sdk/Backplane.globalconfig` sets the analyzer severities and style options, and
`Sdk/Sdk.targets` gives every project whose name ends in `Tests` the test stack (NUnit, NSubstitute, their analyzers, coverage, the usings) with the versions set once here. An application with
central package management moves a version by an `Include` in its own `Directory.Packages.props`; one without gets the versions on the references.

A project uses it by listing it before its normal SDK:

```xml
<Project>
  <Sdk Name="Backplane.Sdk" />
  <Sdk Name="Microsoft.NET.Sdk" />
  ...
</Project>
```

and `global.json` names the version (`"msbuild-sdks": { "Backplane.Sdk": "x.y.z" }`).

**What does not live here:** code, versions of other packages (an application's `Directory.Packages.props`), the `.editorconfig` an editor reads (the template copies it).
**Who may depend on it:** any application's projects. **Tests:** [`tests/Backplane.Sdk.Tests`](../../tests/Backplane.Sdk.Tests/README.md) proves each rule on a sample that breaks it.
**Read more:** [the primer](../../docs/primers/msbuild-sdks.md) and [ADR 0012](../../docs/adr/0012-build-rules-as-an-msbuild-sdk.md).
