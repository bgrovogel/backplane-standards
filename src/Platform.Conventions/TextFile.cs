namespace Platform.Conventions;

/// <summary>One text file of a <see cref="TextSet"/>: where it is and what it says.</summary>
/// <param name="Path">The file's path, as found (or as given, for a sample).</param>
/// <param name="Text">Everything in it.</param>
public sealed record TextFile(string Path, string Text);
