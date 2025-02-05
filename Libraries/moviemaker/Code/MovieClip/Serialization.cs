using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

[JsonConverter( typeof(MovieClipConverter) )]
partial class MovieClip
{
	/// <summary>
	/// Copy editor data so nothing surprising happens if it gets modified.
	/// Returns null if we're not in the editor because we'll never use the data anyway.
	/// </summary>
	internal static JsonObject? CopyEditorData( JsonObject? editorData, JsonSerializerOptions? options )
	{
		return Application.IsEditor ? editorData?.Deserialize<JsonObject>( options ) : null;
	}

	internal MovieTrack AddTrack( MovieTrack.Model model, JsonSerializerOptions? options )
	{
		var track = MovieTrack.Deserialize( this, model, options );

		AddTrackInternal( track );

		return track;
	}
}

file class MovieClipConverter : JsonConverter<MovieClip>
{
	private record Model( IReadOnlyList<MovieTrack.Model> Tracks,
		[property:JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]JsonObject? EditorData = null );

	public override void Write( Utf8JsonWriter writer, MovieClip value, JsonSerializerOptions options )
	{
		var model = new Model( value.AllTracks.Select( x => x.Serialize( options ) ).ToImmutableList(),
			MovieClip.CopyEditorData( value.EditorData, options ) );

		JsonSerializer.Serialize( writer, model, options );
	}

	public override MovieClip Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		var model = JsonSerializer.Deserialize<Model>( ref reader, options )!;
		var clip = new MovieClip { EditorData = MovieClip.CopyEditorData( model.EditorData, options ) };

		foreach ( var trackModel in model.Tracks )
		{
			clip.AddTrack( trackModel, options );
		}

		return clip;
	}
}

partial class MovieTrack
{
	internal record Model( Guid Id, string Name, Type Type,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		Guid? ParentId,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		IReadOnlyList<MovieBlock.Model>? Blocks,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		JsonObject? EditorData );

	internal Model Serialize( JsonSerializerOptions? options ) =>
		new ( Id, Name, PropertyType, Parent?.Id,
			Blocks.Count == 0 ? null : Blocks.Select( x => x.Serialize( options ) ).ToImmutableList(),
			MovieClip.CopyEditorData( EditorData, options ) );

	private MovieBlock AddBlock( MovieBlock.Model model, JsonSerializerOptions? options )
	{
		var block = MovieBlock.Deserialize( this, model, options );

		AddBlockInternal( block );

		return block;
	}

	internal static MovieTrack Deserialize( MovieClip clip, Model model, JsonSerializerOptions? options )
	{
		var parent = model.ParentId is { } parentId ? clip.GetTrack( parentId ) : null;
		var track = new MovieTrack( clip, model.Id, model.Name, model.Type, parent )
		{
			EditorData = MovieClip.CopyEditorData( model.EditorData, options )
		};

		if ( model.Blocks is { } blockModels )
		{
			foreach ( var blockModel in blockModels )
			{
				track.AddBlock( blockModel, options );
			}
		}

		return track;
	}
}

partial class MovieBlock
{
	internal enum Kind
	{
		Constant,
		Samples,
		Action
	}

	internal record Model( int Id, Kind Kind, float StartTime, float? Duration, JsonNode Data );

	private static Kind GetKind( MovieBlockData data )
	{
		return data switch
		{
			IConstantData => Kind.Constant,
			ISamplesData => Kind.Samples,
			ActionData => Kind.Action,
			_ => throw new NotImplementedException()
		};
	}

	internal Model Serialize( JsonSerializerOptions? options )
	{
		var model = new Model( Id, GetKind( Data ), StartTime, Duration,
			JsonSerializer.SerializeToNode( Data, Data.GetType(), options )! );

		return model;
	}

	internal static MovieBlock Deserialize( MovieTrack track, Model model, JsonSerializerOptions? options )
	{
		var dataType = model.Kind switch
		{
			Kind.Constant => TypeLibrary.GetType( typeof( ConstantData<> ) ).MakeGenericType( [track.PropertyType] ),
			Kind.Samples => TypeLibrary.GetType( typeof( SamplesData<> ) ).MakeGenericType( [track.PropertyType] ),
			Kind.Action => typeof( ActionData ),
			_ => throw new NotImplementedException()
		};

		var data = (MovieBlockData)model.Data.Deserialize( dataType, options )!;

		return new MovieBlock( track, model.Id, model.StartTime, model.Duration, data );
	}
}
