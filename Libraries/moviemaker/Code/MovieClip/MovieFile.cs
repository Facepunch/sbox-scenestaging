using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

[JsonPolymorphic]
[JsonDerivedType( typeof(MovieResource), "Resource" )]
[JsonDerivedType( typeof(EmbeddedMovieResource), "Embedded" )]
public interface IMovieSource
{
	/// <summary>
	/// Compiled movie clip.
	/// </summary>
	MovieClip? Clip { get; set; }

	/// <summary>
	/// Editor-only data used to generate <see cref="Clip"/>.
	/// </summary>
	JsonObject? EditorData { get; set; }
}

/// <summary>
/// A <see cref="MovieClip"/> stored as a resource.
/// </summary>
[GameResource( "Movie Clip", "movie", $"A movie clip created with the {nameof(MoviePlayer)} component.", Icon = "video_file" )]
public sealed class MovieResource : GameResource, IMovieSource
{
	/// <inheritdoc />
	[Hide]
	public MovieClip? Clip { get; set; }

	/// <inheritdoc />
	[Hide]
	public JsonObject? EditorData { get; set; }
}

public sealed class EmbeddedMovieResource : IMovieSource
{
	/// <inheritdoc />
	public MovieClip? Clip { get; set; }

	/// <inheritdoc />
	public JsonObject? EditorData { get; set; }
}
