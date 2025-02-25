using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A container for a <see cref="MovieClip"/>, including optional <see cref="EditorData"/>.
/// </summary>
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
	JsonNode? EditorData { get; set; }
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
	public JsonNode? EditorData { get; set; }
}

/// <summary>
/// A <see cref="MovieClip"/> embedded in a property.
/// </summary>
public sealed class EmbeddedMovieResource : IMovieSource
{
	/// <inheritdoc />
	public MovieClip? Clip { get; set; }

	/// <inheritdoc />
	public JsonNode? EditorData { get; set; }
}
