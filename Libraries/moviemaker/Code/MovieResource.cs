using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker.Compiled;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A container for a <see cref="MovieClip"/>, including optional <see cref="EditorData"/>.
/// </summary>
[JsonConverter( typeof(MovieResourceConverter) )]
public interface IMovieResource
{
	/// <summary>
	/// Compiled movie clip.
	/// </summary>
	MovieClip? Compiled { get; set; }

	/// <summary>
	/// Editor-only data used to generate <see cref="Compiled"/>.
	/// </summary>
	JsonNode? EditorData { get; set; }
}

/// <summary>
/// An <see cref="IClip"/> stored on disk as a resource.
/// </summary>
[GameResource( "Movie Clip", "movie", $"A movie clip created with the {nameof(MoviePlayer)} component.", Icon = "video_file" )]
public sealed class MovieResource : GameResource, IMovieResource
{
	/// <inheritdoc />
	[Hide]
	public MovieClip? Compiled { get; set; }

	/// <inheritdoc />
	[Hide]
	public JsonNode? EditorData { get; set; }
}

/// <summary>
/// An <see cref="IClip"/> embedded in a property.
/// </summary>
public sealed class EmbeddedMovieResource : IMovieResource
{
	/// <inheritdoc />
	public MovieClip? Compiled { get; set; }

	/// <inheritdoc />
	public JsonNode? EditorData { get; set; }
}

file sealed class MovieResourceConverter : JsonConverter<IMovieResource>
{
	public override void Write( Utf8JsonWriter writer, IMovieResource value, JsonSerializerOptions options )
	{
		switch ( value )
		{
			case MovieResource resource:
				writer.WriteStringValue( resource.ResourcePath );
				return;

			case EmbeddedMovieResource embedded:
				JsonSerializer.Serialize( writer, embedded, options );
				return;

			default:
				throw new NotImplementedException();
		}
	}

	public override IMovieResource Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		switch ( reader.TokenType )
		{
			case JsonTokenType.String:
				return JsonSerializer.Deserialize<MovieResource>( ref reader, options )!;

			case JsonTokenType.StartObject:
				return JsonSerializer.Deserialize<EmbeddedMovieResource>( ref reader, options )!;

			default:
				throw new Exception( "Expected resource path or embedded resource object." );
		}
	}
}
