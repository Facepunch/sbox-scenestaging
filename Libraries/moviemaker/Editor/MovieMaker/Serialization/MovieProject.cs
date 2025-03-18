using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker.Compiled;
using Sandbox.Utility;

namespace Editor.MovieMaker;

#nullable enable

partial class MovieProject : IJsonPopulator
{
	internal sealed record Model(
		int SampleRate,
		ImmutableDictionary<Guid, ProjectSourceClip.Model> Sources,
		ImmutableDictionary<Guid, IProjectTrack.Model> Tracks );

	public JsonNode Serialize()
	{
		var model = new Model(
			SampleRate,
			SourceClips.ToImmutableDictionary( x => x.Key, x => x.Value.Serialize() ),
			Tracks.ToImmutableDictionary( x => x.Id, x => x.Serialize( EditorJsonOptions ) ) );

		return JsonSerializer.SerializeToNode( model, EditorJsonOptions )!;
	}

	public void Deserialize( JsonNode node )
	{
		throw new NotImplementedException();
	}
}

file class MovieSerializationContext
{
	public static IDisposable Push()
	{
		var old = Current;

		Current = new MovieSerializationContext();

		return new DisposeAction( () =>
		{
			Current = old;
		} );
	}

	[field: ThreadStatic]
	public static MovieSerializationContext? Current { get; private set; }

	private readonly Dictionary<IPropertySignal, int> _signalIds = new();
	private readonly Dictionary<CompiledPropertyBlock, int> _compiledBlockIds = new();

	public void Reset()
	{
		_signalIds.Clear();
	}

	public bool TryRegisterSignal( IPropertySignal signal, out int id )
	{
		if ( _signalIds.TryGetValue( signal, out id ) )
		{
			return false;
		}

		_signalIds[signal] = id = _signalIds.Count + 1;

		return true;
	}
}

partial interface IProjectTrack
{
	[JsonPolymorphic]
	[JsonDerivedType( typeof( IProjectReferenceTrack.Model ), "Reference" )]
	[JsonDerivedType( typeof( IProjectPropertyTrack.Model ), "Property" )]
	public abstract record Model( string Name, Type TargetType,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )] Guid? ParentId );

	Model Serialize( JsonSerializerOptions options );
}

partial class ProjectTrack<T>
{
	public abstract IProjectTrack.Model Serialize( JsonSerializerOptions options );
}

partial interface IProjectReferenceTrack
{
	public sealed record Model( string Name, Type TargetType, Guid? ParentId )
		: IProjectTrack.Model( Name, TargetType, ParentId );
}

partial class ProjectReferenceTrack<T>
{
	public override IProjectTrack.Model Serialize( JsonSerializerOptions options )
	{
		return new IProjectReferenceTrack.Model( Name, TargetType, Parent?.Id );
	}
}

partial interface IProjectPropertyTrack
{
	public sealed record Model( string Name, Type TargetType,
		Guid? ParentId,
		[property: JsonPropertyOrder( 100 ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )] JsonArray? Blocks )
		: IProjectTrack.Model( Name, TargetType, ParentId );
}

partial class ProjectPropertyTrack<T>
{
	public override IProjectTrack.Model Serialize( JsonSerializerOptions options )
	{
		using var contextScope = MovieSerializationContext.Push();

		return new IProjectPropertyTrack.Model( Name, TargetType, Parent?.Id,
			Blocks.Count != 0
				? JsonSerializer.SerializeToNode( Blocks, EditorJsonOptions )!.AsArray()
				: null );
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
	private static string GetDiscriminator( Type type )
	{
		return type.GetCustomAttribute<JsonDiscriminatorAttribute>()?.Value ?? type.Name;
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
		throw new NotImplementedException();
	}
}

[JsonConverter( typeof(ProjectSourceClipConverter) )]
partial record ProjectSourceClip
{
	public record Model( CompiledClip Clip, [property: JsonPropertyOrder( -1 )] JsonObject? Metadata );

	public Model Serialize() => new Model( Clip, Metadata );
}

file class ProjectSourceClipConverter : JsonConverter<ProjectSourceClip>
{
	public override void Write( Utf8JsonWriter writer, ProjectSourceClip value, JsonSerializerOptions options )
	{
		writer.WriteStringValue( value.Id );
	}

	public override ProjectSourceClip? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		throw new NotImplementedException();
	}
}
