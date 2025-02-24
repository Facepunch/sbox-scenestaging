using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

[JsonConverter( typeof( ClipConverter ) )]
partial class CompiledClip;

file sealed class ClipConverter : JsonConverter<CompiledClip>
{
	public override CompiledClip Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		return JsonSerializer.Deserialize<ClipModel>( ref reader, options )!.Deserialize( options );
	}

	public override void Write( Utf8JsonWriter writer, CompiledClip value, JsonSerializerOptions options )
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
	public ClipModel( CompiledClip clip, ImmutableDictionary<CompiledTrack, ImmutableArray<CompiledTrack>> childDict, JsonSerializerOptions? options )
		: this( clip.Tracks is { Length: > 0 }
			? clip.Tracks.Where( x => x.Parent is null ).Select( x => new TrackModel( x, childDict, options ) ).ToImmutableArray()
			: null )
	{

	}

	public CompiledClip Deserialize( JsonSerializerOptions? options )
	{
		return Tracks is { Length: > 0 } rootTracks
			? new CompiledClip( [..rootTracks.SelectMany( x => x.Deserialize( null, options ) )] )
			: CompiledClip.Empty;
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
	public TrackModel( CompiledTrack track, ImmutableDictionary<CompiledTrack, ImmutableArray<CompiledTrack>> childDict, JsonSerializerOptions? options )
		: this( GetKind( track ), track.Name, track.TargetType, (track as IReferenceTrack)?.Id,
			childDict.TryGetValue( track, out var children ) ? children.Select( x => new TrackModel( x, childDict, options ) ).ToImmutableArray() : null,
			track is IBlockTrack { Blocks.Count: > 0 } blockTrack
				? blockTrack.Blocks.Select( x => SerializeBlock( x, options ) ).ToImmutableArray()
				: null )
	{

	}

	public IEnumerable<CompiledTrack> Deserialize( CompiledTrack? parent, JsonSerializerOptions? options )
	{
		var track = Kind switch
		{
			TrackKind.Reference when Type == typeof(GameObject) => new CompiledReferenceTrack<GameObject>(
				Id ?? Guid.NewGuid(), Name, (CompiledReferenceTrack<GameObject>?)parent ),
			TrackKind.Reference => TypeLibrary.GetType( typeof( CompiledReferenceTrack<> ) ).CreateGeneric<CompiledReferenceTrack>( [Type],
				[Id ?? Guid.NewGuid(), Type.Name, (CompiledReferenceTrack<GameObject>?)parent] ),
			TrackKind.Action => new CompiledActionTrack( Name, Type, parent!, ImmutableArray<CompiledActionBlock>.Empty ),
			TrackKind.Property => DeserializeHelper.Get( Type ).DeserializePropertyTrack( this, parent!, options ),
			_ => throw new NotImplementedException()
		};

		return Children is { IsDefaultOrEmpty: false } children
			? [track, ..children.SelectMany( x => x.Deserialize( track, options ) )]
			: [track];
	}

	private static TrackKind GetKind( CompiledTrack track )
	{
		return track switch
		{
			IReferenceTrack => TrackKind.Reference,
			IActionTrack => TrackKind.Action,
			IPropertyTrack => TrackKind.Property,
			_ => throw new NotImplementedException()
		};
	}

	private static JsonObject SerializeBlock( CompiledBlock block, JsonSerializerOptions? options ) =>
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

	public abstract CompiledTrack DeserializePropertyTrack( TrackModel model, CompiledTrack parent, JsonSerializerOptions? options );
}

file sealed class DeserializeHelper<T> : DeserializeHelper
{
	public override CompiledTrack DeserializePropertyTrack( TrackModel model, CompiledTrack parent, JsonSerializerOptions? options )
	{
		return new CompiledPropertyTrack<T>( model.Name, parent,
			model.Blocks?
				.Select( x => DeserializePropertyBlock( x, options ) )
				.ToImmutableArray()
			?? ImmutableArray<CompiledPropertyBlock<T>>.Empty );
	}

	private static CompiledPropertyBlock<T> DeserializePropertyBlock( JsonObject node, JsonSerializerOptions? options )
	{
		var hasSamples = node[nameof( CompiledSampleBlock<object>.Samples )] is not null;

		return hasSamples
			? node.Deserialize<CompiledSampleBlock<T>>( options )!
			: node.Deserialize<CompiledConstantBlock<T>>( options )!;
	}
}
