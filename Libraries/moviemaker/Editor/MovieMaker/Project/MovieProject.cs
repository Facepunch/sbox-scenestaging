using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// All the info needed to compile a <see cref="MovieClip"/>. Gets serialized
/// and stored in <see cref="MovieResource.EditorData"/>.
/// </summary>
public sealed partial class MovieProject
{
	public int SampleRate { get; set; } = 30;

	public MovieClip Compile()
	{
		throw new NotImplementedException();
	}
}
