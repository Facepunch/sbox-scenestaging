using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

[JsonConverter( typeof( MovieTrackConverter ) )]
partial record MovieTrack;

/// <summary>
/// Handles deserializing <see cref="MovieBlock"/>s because they need to match <see cref="MovieTrack.PropertyType"/>.
/// </summary>
file sealed class MovieTrackConverter : JsonConverter<MovieTrack>
{
	public override MovieTrack Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		return JsonSerializer.Deserialize<TrackModel>( ref reader, options )!.Deserialize( options );
	}

	public override void Write( Utf8JsonWriter writer, MovieTrack value, JsonSerializerOptions options )
	{
		JsonSerializer.Serialize( new TrackModel( value, options ), options );
	}

	private record TrackModel( Guid Id, string Name, Type Type,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		ImmutableArray<TrackModel>? Children,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		ImmutableArray<BlockModel>? Blocks )
	{
		public TrackModel( MovieTrack track, JsonSerializerOptions? options )
			: this( track.Id, track.Name, track.PropertyType,
				track.Children is { Length: > 0 }
					? track.Children.Select( x => new TrackModel( x, options ) ).ToImmutableArray()
					: null,
				track.Blocks is { Length: > 0 }
					? track.Blocks.Select( x => new BlockModel( x, options ) ).ToImmutableArray()
					: null )
		{

		}

		public MovieTrack Deserialize( JsonSerializerOptions? options )
		{
			// TODO: handle missing Type?

			return new MovieTrack( Id, Name, Type,
				Children?.Select( x => x.Deserialize( options ) ).ToImmutableArray() ?? [],
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
