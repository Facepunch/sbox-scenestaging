namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A <see cref="MovieClip"/> stored as a resource.
/// </summary>
[GameResource( "Movie Clip", "movie", $"A movie clip created with the {nameof(MoviePlayer)} component.", Icon = "movie" )]
public sealed class MovieFile : GameResource
{
	/// <summary>
	/// The movie clip stored in this resource.
	/// </summary>
	[Hide]
	public MovieClip? Clip { get; set; }
}
