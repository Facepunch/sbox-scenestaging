using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Stores a raw gameplay recording.
/// </summary>
public sealed class ProjectSourceClip( Guid id, string title, CompiledClip clip )
{
	public Guid Id { get; }
	public string Title { get; set; } = title;
	public CompiledClip Clip { get; set; } = clip;
}
