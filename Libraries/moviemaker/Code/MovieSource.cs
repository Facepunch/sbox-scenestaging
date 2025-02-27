using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker.Compiled;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A container for a <see cref="Compiled.Clip"/>, including optional <see cref="EditorData"/>.
/// </summary>
[JsonPolymorphic]
[JsonDerivedType( typeof(MovieResource), "Resource" )]
[JsonDerivedType( typeof(EmbeddedMovieResource), "Embedded" )]
public interface IMovieSource
{
	/// <summary>
	/// Compiled movie clip.
	/// </summary>
	Clip? Clip { get; set; }

	/// <summary>
	/// Editor-only data used to generate <see cref="Clip"/>.
	/// </summary>
	JsonNode? EditorData { get; set; }
}

/// <summary>
/// An <see cref="IClip"/> stored on disk as a resource.
/// </summary>
[GameResource( "Movie Clip", "movie", $"A movie clip created with the {nameof(MoviePlayer)} component.", Icon = "video_file" )]
public sealed class MovieResource : GameResource, IMovieSource
{
	/// <inheritdoc />
	[Hide]
	public Clip? Clip { get; set; }

	/// <inheritdoc />
	[Hide]
	public JsonNode? EditorData { get; set; }
}

/// <summary>
/// An <see cref="IClip"/> embedded in a property.
/// </summary>
public sealed class EmbeddedMovieResource : IMovieSource
{
	/// <inheritdoc />
	public Clip? Clip { get; set; }

	/// <inheritdoc />
	public JsonNode? EditorData { get; set; }
}
