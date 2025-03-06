using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

[JsonConverter( typeof( ClipConverter ) )]
partial class Clip;

file sealed class ClipConverter : JsonConverter<Clip>
{
	public override Clip Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		return JsonSerializer.Deserialize<ClipModel>( ref reader, options )!.Deserialize( options );
	}

	public override void Write( Utf8JsonWriter writer, Clip value, JsonSerializerOptions options )
	{
		var childDict = value.Tracks
			.Where( x => x.Parent is not null )
			.GroupBy( x => x.Parent! )
			.ToImmutableDictionary( x => x.Key, x => x.ToImmutableArray() );

		JsonSerializer.Serialize( writer, new ClipModel( value, childDict, options ), options );
	}
}

[method: JsonConstructor]
file sealed record ClipModel(
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	ImmutableArray<TrackModel>? Tracks )
{
	public ClipModel( Clip clip, ImmutableDictionary<Track, ImmutableArray<Track>> childDict, JsonSerializerOptions? options )
		: this( clip.Tracks is { Length: > 0 }
			? clip.Tracks.Where( x => x.Parent is null ).Select( x => new TrackModel( x, childDict, options ) ).ToImmutableArray()
			: null )
	{

	}

	public Clip Deserialize( JsonSerializerOptions? options )
	{
		return Tracks is { Length: > 0 } rootTracks
			? new Clip( [..rootTracks.SelectMany( x => x.Deserialize( null, options ) )] )
			: Clip.Empty;
	}
}

file enum TrackKind
{
	Reference,
	Action,
	Property
}

[method: JsonConstructor]
file sealed record TrackModel( TrackKind Kind, string Name, Type Type,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )] Guid? Id,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )] ImmutableArray<TrackModel>? Children,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )] ImmutableArray<JsonObject>? Blocks )
{
	public TrackModel( Track track, ImmutableDictionary<Track, ImmutableArray<Track>> childDict, JsonSerializerOptions? options )
		: this( GetKind( track ), track.Name, track.TargetType, (track as IReferenceTrack)?.Id,
			childDict.TryGetValue( track, out var children ) ? [..children.Select( x => new TrackModel( x, childDict, options ) )] : null,
			track is IBlockTrack { Blocks.Count: > 0 } blockTrack
				? blockTrack.Blocks.Select( x => SerializeBlock( x, options ) ).ToImmutableArray()
				: null )
	{

	}

	public IEnumerable<Track> Deserialize( Track? parent, JsonSerializerOptions? options )
	{
		var track = Kind switch
		{
			TrackKind.Reference when Type == typeof(GameObject) => new ReferenceTrack<GameObject>(
				Id ?? Guid.NewGuid(), Name, (ReferenceTrack<GameObject>?)parent ),
			TrackKind.Reference => TypeLibrary.GetType( typeof( ReferenceTrack<> ) ).CreateGeneric<ReferenceTrack>( [Type],
				[Id ?? Guid.NewGuid(), Type.Name, (ReferenceTrack<GameObject>?)parent] ),
			TrackKind.Action => new ActionTrack( Name, Type, parent!, ImmutableArray<ActionBlock>.Empty ),
			TrackKind.Property => DeserializeHelper.Get( Type ).DeserializePropertyTrack( this, parent!, options ),
			_ => throw new NotImplementedException()
		};

		return Children is { IsDefaultOrEmpty: false } children
			? [track, ..children.SelectMany( x => x.Deserialize( track, options ) )]
			: [track];
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
		return new PropertyTrack<T>( model.Name, parent, model.Blocks?.Select( x => DeserializePropertyBlock( x, options ) ).ToImmutableArray() ?? [] );
	}

	private static PropertyBlock<T> DeserializePropertyBlock( JsonObject node, JsonSerializerOptions? options )
	{
		var hasSamples = node[nameof( SampleBlock<object>.Samples )] is not null;

		return hasSamples
			? node.Deserialize<SampleBlock<T>>( options )!
			: node.Deserialize<ConstantBlock<T>>( options )!;
	}
}
