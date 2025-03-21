using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

[JsonConverter( typeof( ClipConverter ) )]
partial class MovieClip;

file sealed class ClipConverter : JsonConverter<MovieClip>
{
	public override MovieClip Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		return JsonSerializer.Deserialize<ClipModel>( ref reader, options )!.Deserialize( options );
	}

	public override void Write( Utf8JsonWriter writer, MovieClip value, JsonSerializerOptions options )
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
	public ClipModel( MovieClip clip, ImmutableDictionary<ICompiledTrack, ImmutableArray<ICompiledTrack>> childDict, JsonSerializerOptions? options )
		: this( clip.Tracks is { Length: > 0 }
			? clip.Tracks.Where( x => x.Parent is null ).Select( x => new TrackModel( x, childDict, options ) ).ToImmutableArray()
			: null )
	{

	}

	public MovieClip Deserialize( JsonSerializerOptions? options )
	{
		return Tracks is { Length: > 0 } rootTracks
			? MovieClip.FromTracks( rootTracks.SelectMany( x => x.Deserialize( null, options ) ) )
			: MovieClip.Empty;
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
	public TrackModel( ICompiledTrack track, ImmutableDictionary<ICompiledTrack, ImmutableArray<ICompiledTrack>> childDict, JsonSerializerOptions? options )
		: this( GetKind( track ), track.Name, track.TargetType, (track as IReferenceTrack)?.Id,
			childDict.TryGetValue( track, out var children ) ? children.Select( x => new TrackModel( x, childDict, options ) ).ToImmutableArray() : null,
			track is ICompiledBlockTrack { Blocks.Count: > 0 } blockTrack
				? blockTrack.Blocks.Select( x => SerializeBlock( x, options ) ).ToImmutableArray()
				: null )
	{

	}

	public IEnumerable<ICompiledTrack> Deserialize( ICompiledTrack? parent, JsonSerializerOptions? options )
	{
		var track = Kind switch
		{
			TrackKind.Reference when Type == typeof(GameObject) => new CompiledReferenceTrack<GameObject>(
				Id ?? Guid.NewGuid(), Name, (CompiledReferenceTrack<GameObject>?)parent ),
			TrackKind.Reference => TypeLibrary.GetType( typeof( CompiledReferenceTrack<> ) ).CreateGeneric<ICompiledReferenceTrack>( [Type],
				[Id ?? Guid.NewGuid(), Type.Name, (CompiledReferenceTrack<GameObject>?)parent] ),
			TrackKind.Action => new CompiledActionTrack( Name, Type, parent!, ImmutableArray<CompiledActionBlock>.Empty ),
			TrackKind.Property => DeserializeHelper.Get( Type ).DeserializePropertyTrack( this, parent!, options ),
			_ => throw new NotImplementedException()
		};

		return Children is { IsDefaultOrEmpty: false } children
			? [track, ..children.SelectMany( x => x.Deserialize( track, options ) )]
			: [track];
	}

	private static TrackKind GetKind( ICompiledTrack track )
	{
		return track switch
		{
			IReferenceTrack => TrackKind.Reference,
			IActionTrack => TrackKind.Action,
			IPropertyTrack => TrackKind.Property,
			_ => throw new NotImplementedException()
		};
	}

	private static JsonObject SerializeBlock( ICompiledBlock block, JsonSerializerOptions? options ) =>
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

	public abstract ICompiledTrack DeserializePropertyTrack( TrackModel model, ICompiledTrack parent, JsonSerializerOptions? options );
}

file sealed class DeserializeHelper<T> : DeserializeHelper
{
	public override ICompiledTrack DeserializePropertyTrack( TrackModel model, ICompiledTrack parent, JsonSerializerOptions? options )
	{
		return new CompiledPropertyTrack<T>( model.Name, parent,
			model.Blocks?
				.Select( x => DeserializePropertyBlock( x, options ) )
				.ToImmutableArray()
			?? ImmutableArray<ICompiledPropertyBlock<T>>.Empty );
	}

	private static ICompiledPropertyBlock<T> DeserializePropertyBlock( JsonObject node, JsonSerializerOptions? options )
	{
		var hasSamples = node[nameof( CompiledSampleBlock<object>.Samples )] is not null;

		return hasSamples
			? node.Deserialize<CompiledSampleBlock<T>>( options )!
			: node.Deserialize<CompiledConstantBlock<T>>( options )!;
	}
}

[JsonConverter( typeof(CompiledSampleBlockConverterFactory) )]
partial record CompiledSampleBlock<T>;

file sealed class CompiledSampleBlockConverterFactory : JsonConverterFactory
{
	public override bool CanConvert( Type typeToConvert ) =>
		TypeLibrary.GetType( typeToConvert )?.TargetType == typeof(CompiledSampleBlock<>);

	public override JsonConverter? CreateConverter( Type typeToConvert, JsonSerializerOptions options )
	{
		var valueType = TypeLibrary.GetGenericArguments( typeToConvert )[0];

		try
		{
			return TypeLibrary.GetType( typeof(CompressedSampleBlockConverter<>) )
				.CreateGeneric<JsonConverter>( [valueType] );
		}
		catch
		{
			return JsonSerializerOptions.Default.GetConverter( typeof(int) );
		}
	}
}

file sealed class CompressedSampleBlockConverter<T> : JsonConverter<CompiledSampleBlock<T>>
	where T : unmanaged
{
	private sealed record Model( MovieTimeRange TimeRange,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )] MovieTime Offset,
		int SampleRate, JsonNode Samples );

	public override void Write( Utf8JsonWriter writer, CompiledSampleBlock<T> value, JsonSerializerOptions options )
	{
		var stream = ByteStream.Create( 16 * value.Samples.Length + 4 );

		stream.WriteArray( value.Samples.AsSpan() );

		var compressed = stream.Compress();
		var base64 = Convert.ToBase64String( compressed.ToArray() );
		var model = new Model( value.TimeRange, value.Offset, value.SampleRate, base64 );

		JsonSerializer.Serialize( writer, model, options );
	}

	public override CompiledSampleBlock<T> Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		var model = JsonSerializer.Deserialize<Model>( ref reader, options )!;

		ImmutableArray<T> samples;

		if ( model.Samples is JsonArray sampleArray )
		{
			samples = sampleArray.Deserialize<ImmutableArray<T>>( options );
		}
		else if ( model.Samples.GetValue<string>() is { } base64 )
		{
			var compressed = ByteStream.CreateReader( Convert.FromBase64String( base64 ) );
			var stream = compressed.Decompress();

			samples = stream.ReadArray<T>( 0x10_0000 ).ToImmutableArray();
		}
		else
		{
			throw new Exception( "Expected array or compressed sample string." );
		}

		return new CompiledSampleBlock<T>( model.TimeRange, model.Offset, model.SampleRate, samples );
	}
}
