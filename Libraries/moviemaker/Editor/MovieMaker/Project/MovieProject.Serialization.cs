using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

[JsonConverter( typeof(MovieProjectConverter) )]
partial class MovieProject : IJsonPopulator
{
	internal sealed record Model(
		int SampleRate, MovieTime? Duration,
		ImmutableDictionary<Guid, ProjectTrackModel> Tracks,
		ImmutableDictionary<Guid, ProjectSourceClip.Model>? Sources );

	public JsonNode Serialize() => JsonSerializer.SerializeToNode( Snapshot(), EditorJsonOptions )!;

	internal Model Snapshot()
	{
		using var scope = MovieSerializationContext.Push();

		return new Model(
			SampleRate, Duration,
			Tracks.ToImmutableDictionary( x => x.Id, x => x.Serialize( EditorJsonOptions ) ),
			scope.SourceClips.ToImmutableDictionary( x => x.Id, x => x.Serialize() ) );
	}

	internal void Restore( Model model )
	{
		using var scope = MovieSerializationContext.Push();

		SampleRate = model.SampleRate;

		foreach ( var (id, sourceModel) in model.Sources ?? ImmutableDictionary<Guid, ProjectSourceClip.Model>.Empty )
		{
			scope.RegisterSourceClip( new ProjectSourceClip( id, sourceModel.Clip, sourceModel.Metadata ) );
		}

		_rootTrackList.Clear();
		_trackDict.Clear();
		_trackList.Clear();
		_tracksChanged = true;

		var addedTracks = new Dictionary<Guid, IProjectTrack>();

		foreach ( var (id, trackModel) in model.Tracks )
		{
			switch ( trackModel )
			{
				case ProjectReferenceTrackModel refModel:
					addedTracks[id] = IProjectReferenceTrack.Create( this, id, refModel.Name, refModel.TargetType );
					break;

				case ProjectPropertyTrackModel propertyModel:
					addedTracks[id] = IProjectPropertyTrack.Create( this, id, propertyModel.Name, propertyModel.TargetType );
					break;

				case ProjectSequenceTrackModel sequenceModel:
					addedTracks[id] = new ProjectSequenceTrack( this, id, sequenceModel.Name );
					break;

				default:
					throw new NotImplementedException();
			}
		}

		foreach ( var (id, trackModel) in model.Tracks )
		{
			var addedTrack = addedTracks[id];
			var parentTrack = trackModel.ParentId is { } parentId ? addedTracks[parentId] : null;

			AddTrackInternal( addedTrack, parentTrack );
		}

		foreach ( var (id, trackModel) in model.Tracks )
		{
			var addedTrack = addedTracks[id];

			addedTrack.Deserialize( trackModel, EditorJsonOptions );
		}
	}

	public void Deserialize( JsonNode node ) =>
		Restore( node.Deserialize<Model>( EditorJsonOptions )! );
}

file sealed class MovieProjectConverter : JsonConverter<MovieProject>
{
	public override void Write( Utf8JsonWriter writer, MovieProject value, JsonSerializerOptions options )
	{
		JsonSerializer.Serialize( value.Serialize(), options );
	}

	public override MovieProject? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		var project = new MovieProject();
		var node = JsonSerializer.Deserialize<JsonNode>( ref reader, options )!;

		project.Deserialize( node );

		return project;
	}
}

file sealed class MovieSerializationContext : IDisposable
{
	public static MovieSerializationContext Push()
	{
		return Current = new MovieSerializationContext( Current );
	}

	[field: ThreadStatic]
	public static MovieSerializationContext? Current { get; private set; }

	private readonly Dictionary<IPropertySignal, int> _signalsToId = new();
	private readonly Dictionary<int, IPropertySignal> _signalsFromId = new();

	private readonly Dictionary<Guid, ProjectSourceClip> _sources = new();

	private readonly MovieSerializationContext? _parent;

	public IEnumerable<ProjectSourceClip> SourceClips => _sources.Values.OrderBy( x => x.Id );

	private MovieSerializationContext( MovieSerializationContext? parent )
	{
		_parent = parent;
	}

	public void ResetSignals()
	{
		_signalsToId.Clear();
		_signalsFromId.Clear();
	}

	public bool TryRegisterSignal( IPropertySignal signal, out int id )
	{
		if ( _signalsToId.TryGetValue( signal, out id ) )
		{
			return false;
		}

		_signalsToId[signal] = id = _signalsToId.Count + 1;

		return true;
	}

	public void RegisterSignal( int id, IPropertySignal signal ) => _signalsFromId[id] = signal;
	public IPropertySignal GetSignal( int id ) => _signalsFromId[id];
	public ProjectSourceClip GetSourceClip( Guid id ) => _sources[id];

	public void Dispose()
	{
		if ( Current != this ) throw new InvalidOperationException();

		Current = _parent;
	}

	public void RegisterSourceClip( ProjectSourceClip value )
	{
		_sources[value.Id] = value;
	}
}

[JsonPolymorphic]
[JsonDerivedType( typeof( ProjectReferenceTrackModel ), "Reference" )]
[JsonDerivedType( typeof( ProjectPropertyTrackModel ), "Property" )]
[JsonDerivedType( typeof( ProjectSequenceTrackModel ), "Sequence" )]
public abstract record ProjectTrackModel( string Name, Type TargetType,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )] Guid? ParentId );

file sealed record ProjectReferenceTrackModel( string Name, Type TargetType, Guid? ParentId )
	: ProjectTrackModel( Name, TargetType, ParentId );

file sealed record ProjectPropertyTrackModel( string Name, Type TargetType, Guid? ParentId,
	[property: JsonPropertyOrder( 100 ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )] JsonArray? Blocks )
	: ProjectTrackModel( Name, TargetType, ParentId );

file sealed record ProjectSequenceTrackModel( string Name, Guid? ParentId, ImmutableArray<ProjectSequenceBlock> Blocks )
	: ProjectTrackModel( Name, typeof( MovieResource ), ParentId );

partial interface IProjectTrack
{
	ProjectTrackModel Serialize( JsonSerializerOptions options );
	void Deserialize( ProjectTrackModel model, JsonSerializerOptions options );
}

partial class ProjectTrack<T>
{
	public abstract ProjectTrackModel Serialize( JsonSerializerOptions options );
	public abstract void Deserialize( ProjectTrackModel model, JsonSerializerOptions options );
}

partial class ProjectReferenceTrack<T>
{
	public override ProjectTrackModel Serialize( JsonSerializerOptions options )
	{
		return new ProjectReferenceTrackModel( Name, TargetType, Parent?.Id );
	}

	public override void Deserialize( ProjectTrackModel model, JsonSerializerOptions options )
	{
		if ( model is not ProjectReferenceTrackModel refModel ) return;
	}
}

partial class ProjectPropertyTrack<T>
{
	public override ProjectTrackModel Serialize( JsonSerializerOptions options )
	{
		MovieSerializationContext.Current?.ResetSignals();

		return new ProjectPropertyTrackModel( Name, TargetType, Parent?.Id,
			Blocks.Count != 0
				? JsonSerializer.SerializeToNode( Blocks, EditorJsonOptions )!.AsArray()
				: null );
	}

	public override void Deserialize( ProjectTrackModel model, JsonSerializerOptions options )
	{
		if ( model is not ProjectPropertyTrackModel propertyModel ) return;

		MovieSerializationContext.Current?.ResetSignals();

		_blocks.Clear();
		_blocksChanged = true;

		if ( propertyModel.Blocks?.Deserialize<ImmutableArray<PropertyBlock<T>>>( options ) is not { } blocks )
		{
			return;
		}

		_blocks.AddRange( blocks );
	}
}

partial class ProjectSequenceTrack
{
	public override ProjectTrackModel Serialize( JsonSerializerOptions options )
	{
		return new ProjectSequenceTrackModel( Name, Parent?.Id, [..Blocks] );
	}

	public override void Deserialize( ProjectTrackModel model, JsonSerializerOptions options )
	{
		if ( model is not ProjectSequenceTrackModel sequenceModel ) return;

		_tracksInvalid = true;

		_blocks.Clear();

		if ( sequenceModel.Blocks is { IsDefaultOrEmpty: false } blocks )
		{
			_blocks.AddRange( blocks );
		}
	}
}

public sealed class JsonDiscriminatorAttribute( string value ) : Attribute
{
	public string Value { get; } = value;
}

[JsonConverter( typeof(PropertySignalConverterFactory) )]
partial record PropertySignal<T>;

file class PropertySignalConverterFactory : JsonConverterFactory
{
	public override bool CanConvert( Type typeToConvert ) =>
		typeToConvert.IsConstructedGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(PropertySignal<>);

	public override JsonConverter CreateConverter( Type typeToConvert, JsonSerializerOptions options )
	{
		var valueType = typeToConvert.GetGenericArguments()[0];
		var converterType = typeof(PropertySignalConverter<>).MakeGenericType( valueType );

		return (JsonConverter)Activator.CreateInstance( converterType )!;
	}
}

file class PropertySignalConverter<T> : JsonConverter<PropertySignal<T>>
{
	[SkipHotload]
	private static ImmutableDictionary<string, Type>? _discriminatorLookup;

	private static string GetDiscriminator( Type type )
	{
		return type.GetCustomAttribute<JsonDiscriminatorAttribute>()?.Value ?? type.Name;
	}

	private static Type GetType( string discriminator )
	{
		_discriminatorLookup ??= EditorTypeLibrary.GetTypesWithAttribute<JsonDiscriminatorAttribute>()
			.Where( x => !x.Type.IsAbstract && x.Type.IsGenericType )
			.Select( x => (Name: x.Attribute.Value, Type: x.Type.TargetType.MakeGenericType( typeof(T) )) )
			.ToImmutableDictionary( x => x.Name, x => x.Type );

		return _discriminatorLookup[discriminator];
	}

	public override void Write( Utf8JsonWriter writer, PropertySignal<T> value, JsonSerializerOptions options )
	{
		var context = MovieSerializationContext.Current!;

		if ( !context.TryRegisterSignal( value, out var id ) )
		{
			writer.WriteNumberValue( id );
			return;
		}

		var type = value.GetType();
		var node = JsonSerializer.SerializeToNode( value, type, options )!.AsObject();

		node.Insert( 0, "$id", id );
		node.Insert( 1, "$type", GetDiscriminator( type ) );

		JsonSerializer.Serialize( writer, node, options );
	}

	public override PropertySignal<T>? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		if ( reader.TokenType == JsonTokenType.Number )
		{
			var refId = JsonSerializer.Deserialize<int>( ref reader, options );

			return (PropertySignal<T>)MovieSerializationContext.Current!.GetSignal( refId );
		}

		var node = JsonSerializer.Deserialize<JsonObject>( ref reader, options )!;
		var id = node["$id"]!.GetValue<int>();
		var discriminator = node["$type"]!.GetValue<string>();
		var type = GetType( discriminator );

		var signal = (PropertySignal<T>)node.Deserialize( type, options )!;

		MovieSerializationContext.Current!.RegisterSignal( id, signal );

		return signal;
	}
}

[JsonConverter( typeof(ProjectSourceClipConverter) )]
partial record ProjectSourceClip
{
	public record Model( MovieClip Clip, [property: JsonPropertyOrder( -1 )] JsonObject? Metadata );

	public Model Serialize() => new ( Clip, Metadata );
}

file class ProjectSourceClipConverter : JsonConverter<ProjectSourceClip>
{
	public override void Write( Utf8JsonWriter writer, ProjectSourceClip value, JsonSerializerOptions options )
	{
		MovieSerializationContext.Current?.RegisterSourceClip( value );

		writer.WriteStringValue( value.Id );
	}

	public override ProjectSourceClip? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		var id = JsonSerializer.Deserialize<Guid>( ref reader, options );

		return MovieSerializationContext.Current!.GetSourceClip( id );
	}
}
