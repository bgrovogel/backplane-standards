using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Platform.Sdk.Tests;

/// <summary>
/// A small solution made in a temporary folder that gets its build rules from Platform.Sdk, packed from this repository into a folder feed of its own, so a rule is proved by a
/// sample that breaks it. Everything stays inside the temporary folder, the package cache included, so nothing is left behind and a changed SDK is never read from an old cache.
/// </summary>
internal sealed class SampleBuild : IDisposable
{
    private static readonly Lazy<(string Feed, string Version, string Cache)> Packed = new(PackTheSdk);

    private readonly string _folder = Directory.CreateTempSubdirectory("platform-sdk-sample-").FullName;

    public SampleBuild()
    {
        (string feed, string version, _) = Packed.Value;
        Write("global.json", $$"""{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" }, "msbuild-sdks": { "Platform.Sdk": "{{version}}" } }""");
        // The SDK comes from the folder feed; the test packages come from this machine's own cache when it has them, and from nuget.org when it does not.
        Write(
            "nuget.config",
            $"""
            <configuration>
              <packageSources><clear /><add key="feed" value="{feed}" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
              <fallbackPackageFolders><add key="machine" value="{MachinePackages()}" /></fallbackPackageFolders>
            </configuration>
            """);
    }

    /// <summary>Writes a file under the sample's folder.</summary>
    public SampleBuild Write(string relativePath, string content)
    {
        string path = Path.Combine(_folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return this;
    }

    /// <summary>The text of a file under the sample's folder, as the build left it.</summary>
    public string Read(string relativePath) => File.ReadAllText(Path.Combine(_folder, relativePath));

    /// <summary>Adds a project that takes its rules from Platform.Sdk, with one source file.</summary>
    public SampleBuild WithProject(string name, string code, string extraProperties = "", string outputType = "Library")
    {
        Write(
            $"{name}/{name}.csproj",
            $"""
            <Project>
              <Sdk Name="Platform.Sdk" />
              <Sdk Name="Microsoft.NET.Sdk" />
              <PropertyGroup><OutputType>{outputType}</OutputType>{extraProperties}</PropertyGroup>
            </Project>
            """);
        return Write($"{name}/Code.cs", code);
    }

    /// <summary>Builds a project of the sample and says what happened.</summary>
    public BuildResult Build(string project) => Run("build", project);

    /// <summary>Runs the tests of a project of the sample and says what happened.</summary>
    public BuildResult Test(string project) => Run("test", project);

    private BuildResult Run(string verb, string project)
    {
        var start = new ProcessStartInfo("dotnet", $"{verb} \"{Path.Combine(_folder, project, project + ".csproj")}\" -nologo -v {(verb == "test" ? "m" : "q")} -nodeReuse:false -p:UseSharedCompilation=false")
        {
            WorkingDirectory = _folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["NUGET_PACKAGES"] = Packed.Value.Cache;
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string[] files = [.. Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_folder, path).Replace('\\', '/'))];
        return new BuildResult(process.ExitCode, output.Result + errors.Result, files);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A build server may still hold a file; the operating system clears the temporary folder in time.
        }
    }

    /// <summary>Packs Platform.Sdk from this repository into a folder feed, under a version no earlier run has used.</summary>
    private static (string Feed, string Version, string Cache) PackTheSdk()
    {
        string feed = Directory.CreateTempSubdirectory("platform-sdk-feed-").FullName;
        string cache = Directory.CreateTempSubdirectory("platform-sdk-cache-").FullName;
        string version = $"0.0.0-test.{Guid.NewGuid():N}";
        var start = new ProcessStartInfo("dotnet", $"pack \"{Path.Combine(RepositoryRoot(), "src", "Platform.Sdk")}\" -nologo -v q -o \"{feed}\" -p:Version={version}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Packing Platform.Sdk failed:\n{output.Result}{errors.Result}");
        }

        return (feed, version, cache);
    }

    /// <summary>This machine's own package cache: where restore finds NUnit and the rest without the network, when it has them.</summary>
    private static string MachinePackages() =>
        Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
}
