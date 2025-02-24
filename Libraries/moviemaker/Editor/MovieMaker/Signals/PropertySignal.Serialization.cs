using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Editor.MovieMaker;

#nullable enable

[JsonConverter( typeof(PropertySignalConverterFactory) )]
partial record PropertySignal<T>
{
	public JsonNode? Serialize()
	{
		PropertyBlockReferenceConverter<T>.Reset();

		return JsonSerializer.SerializeToNode( this, EditorJsonOptions )!;
	}
}

public sealed class JsonDiscriminatorAttribute( string value ) : Attribute
{
	public string Value { get; } = value;
}

file sealed class PropertyBlockReferenceConverter<T>
	: JsonConverter<PropertyBlock<T>>
{
	[ThreadStatic] private static Dictionary<PropertyBlock<T>, int>? _idDict;

	private static string GetDiscriminator( Type type )
	{
		return type.GetCustomAttribute<JsonDiscriminatorAttribute>()?.Value ?? type.Name;
	}

	public static void Reset()
	{
		_idDict ??= new Dictionary<PropertyBlock<T>, int>();
		_idDict.Clear();
	}

	public override PropertyBlock<T>? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		throw new NotImplementedException();
	}

	public override void Write( Utf8JsonWriter writer, PropertyBlock<T> value, JsonSerializerOptions options )
	{
		var type = value.GetType();

		if ( _idDict is not { } idDict )
		{
			JsonSerializer.Serialize( writer, value, type, options );
			return;
		}

		if ( idDict.TryGetValue( value, out var id ) )
		{
			JsonSerializer.Serialize( writer, id, options );
			return;
		}

		id = idDict.Count + 1;

		idDict.Add( value, id );

		var node = JsonSerializer.SerializeToNode( value, type, options )!.AsObject();

		node.Insert( 0, "$id", id );
		node.Insert( 1, "$type", GetDiscriminator( type ) );

		JsonSerializer.Serialize( writer, node, options );
	}
}

internal sealed class PropertySignalConverterFactory : JsonConverterFactory
{
	public override bool CanConvert( Type typeToConvert )
	{
		while ( true )
		{
			if ( typeToConvert is not { IsAbstract: true, IsConstructedGenericType: true } ) return false;
			if ( typeToConvert.GetGenericTypeDefinition() == typeof(PropertyBlock<>) ) return true;

			typeToConvert = typeToConvert.BaseType!;
		}
	}

	public override JsonConverter CreateConverter( Type typeToConvert, JsonSerializerOptions options )
	{
		var valueType = typeToConvert.GetGenericArguments()[0];

		return (JsonConverter)Activator.CreateInstance( typeof( PropertyBlockReferenceConverter<>).MakeGenericType( valueType ) )!;
	}
}

file sealed class PropertySignalConverter<T> : JsonConverter<PropertySignal<T>>
{
	[SkipHotload]
	private ImmutableDictionary<string, Type>? _types;

	private Type? GetType( string? discriminator )
	{
		if ( discriminator is null ) return null;

		_types ??= FindTypes();

		return _types.GetValueOrDefault( discriminator );
	}

	private ImmutableDictionary<string, Type> FindTypes()
	{
		return EditorTypeLibrary.GetTypesWithAttribute<JsonDiscriminatorAttribute>()
			.ToImmutableDictionary( x => x.Attribute.Value, x => x.Type.TargetType );
	}

	private static string GetDiscriminator( Type type )
	{
		return type.GetCustomAttribute<JsonDiscriminatorAttribute>()?.Value ?? type.Name;
	}

	public override PropertySignal<T> Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		var obj = JsonSerializer.Deserialize<JsonObject>( ref reader, options );
		var discriminator = obj?["$type"]?.GetValue<string>();

		if ( GetType( discriminator ) is not { } type )
		{
			throw new Exception( $"Unrecognized block type \"{discriminator ?? "null"}\"." );
		}

		return (PropertySignal<T>)obj.Deserialize( type, options )!;
	}

	public override void Write( Utf8JsonWriter writer, PropertySignal<T> value, JsonSerializerOptions options )
	{
		var type = value.GetType();
		var node = JsonSerializer.SerializeToNode( value, type, options )!.AsObject();

		node.Insert( 0, "$type", GetDiscriminator( type ) );

		JsonSerializer.Serialize( writer, node, options );
	}
}
