namespace Platform.Conventions;

/// <summary>What one method that is past a limit is allowed to be today, so it can only shrink: a ratchet.</summary>
/// <param name="Lines">The most lines.</param>
/// <param name="Complexity">The most paths.</param>
public sealed record Ceiling(int Lines, int Complexity);
