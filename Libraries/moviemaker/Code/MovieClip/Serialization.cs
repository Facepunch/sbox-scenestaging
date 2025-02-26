using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

[JsonConverter( typeof( MovieClipConverter ) )]
partial record MovieClip;

file sealed class MovieClipConverter : JsonConverter<MovieClip>
{
	public override MovieClip Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		return JsonSerializer.Deserialize<ClipModel>( ref reader, options )!.Deserialize( options );
	}

	public override void Write( Utf8JsonWriter writer, MovieClip value, JsonSerializerOptions options )
	{
		JsonSerializer.Serialize( new ClipModel( value, options ), options );
	}

	private record ClipModel(
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		ImmutableArray<TrackModel>? Tracks )
	{
		public ClipModel( MovieClip clip, JsonSerializerOptions? options )
			: this( clip.Tracks is { Length: > 0 }
				? clip.Tracks.OrderBy( x => x.Depth ).ThenBy( x => x.Name ).Select( x => new TrackModel( x, options ) ).ToImmutableArray()
				: null )
		{

		}

		public MovieClip Deserialize( JsonSerializerOptions? options )
		{
			if ( Tracks is not { Length: > 0 } trackModels ) return MovieClip.Empty;

			var trackDict = new Dictionary<Guid, MovieTrack>();

			return new MovieClip( [..trackModels.Select( x => x.Deserialize( trackDict, options ) )] );
		}
	}

	private record TrackModel( Guid Id, string Name, Type Type,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		Guid? ParentId,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		ImmutableArray<BlockModel>? Blocks )
	{
		public TrackModel( MovieTrack track, JsonSerializerOptions? options )
			: this( track.Id, track.Name, track.TargetType, track.Parent?.Id,
				track.Blocks is { Length: > 0 }
					? track.Blocks.Select( x => new BlockModel( x, options ) ).ToImmutableArray()
					: null )
		{

		}

		public MovieTrack Deserialize( IReadOnlyDictionary<Guid, MovieTrack> trackDict, JsonSerializerOptions? options )
		{
			// TODO: handle missing Type?

			return new MovieTrack( Id, Name, Type,
				ParentId is { } parentId ? trackDict[parentId] : null,
				Blocks?.Select( x => x.Deserialize( Type, options ) ).ToImmutableArray() ?? [] );
		}
	}

	private enum BlockKind
	{
		Constant,
		Samples,
		Action
	}

	private record BlockModel( BlockKind Kind, MovieTimeRange TimeRange, JsonNode Data )
	{
		private static BlockKind GetKind( IBlockData data )
		{
			return data switch
			{
				IConstantData => BlockKind.Constant,
				ISamplesData => BlockKind.Samples,
				ActionData => BlockKind.Action,
				_ => throw new NotImplementedException()
			};
		}

		public BlockModel( MovieBlock block, JsonSerializerOptions? options )
			: this( GetKind( block.Data ), block.TimeRange,
				JsonSerializer.SerializeToNode( block.Data, block.Data.GetType(), options )! )
		{

		}

		public MovieBlock Deserialize( Type propertyType, JsonSerializerOptions? options )
		{
			var dataType = Kind switch
			{
				BlockKind.Constant => TypeLibrary.GetType( typeof(ConstantData<>) ).MakeGenericType( [propertyType] ),
				BlockKind.Samples => TypeLibrary.GetType( typeof(SamplesData<>) ).MakeGenericType( [propertyType] ),
				BlockKind.Action => typeof(ActionData),
				_ => throw new NotImplementedException()
			};

			var data = (IBlockData)Data.Deserialize( dataType, options )!;

			return new MovieBlock( TimeRange, data );
		}
	}
}
