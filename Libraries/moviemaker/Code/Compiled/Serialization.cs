using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

[JsonConverter( typeof( ClipConverter ) )]
partial record Clip;

file sealed class ClipConverter : JsonConverter<Clip>
{
	public override Clip Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		return JsonSerializer.Deserialize<ClipModel>( ref reader, options )!.Deserialize( options );
	}

	public override void Write( Utf8JsonWriter writer, Clip value, JsonSerializerOptions options )
	{
		JsonSerializer.Serialize( new ClipModel( value, options ), options );
	}
}

file sealed record ClipModel(
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	ImmutableArray<TrackModel>? Tracks )
{
	public ClipModel( Clip clip, JsonSerializerOptions? options )
		: this( clip.Tracks is { Length: > 0 }
			? clip.Tracks.OrderBy( x => x.GetDepth() ).ThenBy( x => x.Name ).Select( x => new TrackModel( x, options ) ).ToImmutableArray()
			: null )
	{

	}

	public Clip Deserialize( JsonSerializerOptions? options )
	{
		if ( Tracks is not { Length: > 0 } trackModels ) return Clip.Empty;

		var trackDict = new Dictionary<Guid, Track>();

		return new Clip( [.. trackModels.Select( x => x.Deserialize( trackDict, options ) )] );
	}
}

file enum TrackKind
{
	Reference,
	Action,
	Property
}

file sealed record TrackModel( TrackKind Kind, Guid Id, string Name, Type Type,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		Guid? ParentId,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		ImmutableArray<JsonObject>? Blocks )
{
	public TrackModel( Track track, JsonSerializerOptions? options )
		: this( GetKind( track ), track.Id, track.Name, track.TargetType, track.Parent?.Id,
			track is IBlockTrack<Block> { Blocks.Count: > 0 } blockTrack
				? blockTrack.Blocks.Select( x => SerializeBlock( x, options ) ).ToImmutableArray()
				: null )
	{

	}

	public Track Deserialize( IReadOnlyDictionary<Guid, Track> trackDict, JsonSerializerOptions? options )
	{
		var parent = ParentId is { } parentId ? trackDict[parentId] : null;

		return Kind switch
		{
			TrackKind.Reference => new ReferenceTrack( Id, Name, Type, parent ),
			TrackKind.Action => new ActionTrack( Id, Name, Type, parent ),
			TrackKind.Property => DeserializeHelper.Get( Type ).DeserializePropertyTrack( this, parent!, options ),
			_ => throw new NotImplementedException()
		};
	}

	private static TrackKind GetKind( Track track )
	{
		return track switch
		{
			IReferenceTrack => TrackKind.Reference,
			IActionTrack => TrackKind.Action,
			IPropertyTrack => TrackKind.Property,
			_ => throw new NotImplementedException()
		};
	}

	private static JsonObject SerializeBlock( Block block, JsonSerializerOptions? options ) =>
		JsonSerializer.SerializeToNode( block, block.GetType(), options )!.AsObject();
}

file abstract class DeserializeHelper
{
	[SkipHotload]
	private static Dictionary<Type, DeserializeHelper> Cache { get; } = new();

	public static DeserializeHelper Get( Type type )
	{
		if ( Cache.TryGetValue( type, out var cached ) ) return cached;

		return Cache[type] = TypeLibrary.GetType( typeof(DeserializeHelper<>) )
			.CreateGeneric<DeserializeHelper>( [type] );
	}

	public abstract Track DeserializePropertyTrack( TrackModel model, Track parent, JsonSerializerOptions? options );
}

file sealed class DeserializeHelper<T> : DeserializeHelper
{
	public override Track DeserializePropertyTrack( TrackModel model, Track parent, JsonSerializerOptions? options )
	{
		return new PropertyTrack<T>( model.Id, model.Name, parent, model.Blocks?.Select( x => DeserializePropertyBlock( x, options ) ).ToImmutableArray() ?? [] );
	}

	private static PropertyBlock<T> DeserializePropertyBlock( JsonObject node, JsonSerializerOptions? options )
	{
		var hasSamples = node[nameof( ISampleBlock.Samples )] is not null;

		return hasSamples
			? node.Deserialize<SampleBlock<T>>( options )!
			: node.Deserialize<ConstantBlock<T>>( options )!;
	}
}
