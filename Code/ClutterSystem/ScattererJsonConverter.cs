using System.Text.Json;

namespace Sandbox.Clutter;

/// <summary>
/// Custom JSON converter for polymorphic Scatterer serialization.
/// </summary>
public class ScattererJsonConverter : IJsonConvert
{
	public static object JsonRead( ref Utf8JsonReader reader, Type typeToConvert )
	{
		using var doc = JsonDocument.ParseValue( ref reader );
		var root = doc.RootElement;

		// Get the type name
		if ( !root.TryGetProperty( "$type", out var typeElement ) )
		{
			Log.Warning( "Scatterer JSON missing $type property, using SimpleScatterer" );
			return new SimpleScatterer();
		}

		var typeName = typeElement.GetString();
		var scattererType = TypeLibrary.GetTypes()
			.FirstOrDefault( t => t.Name == typeName && t.TargetType?.IsAssignableTo( typeof( Scatterer ) ) is true );

		if ( scattererType is null )
		{
			Log.Warning( $"Scatterer type '{typeName}' not found, using SimpleScatterer" );
			return new SimpleScatterer();
		}

		try
		{
			var instance = TypeLibrary.Create<Scatterer>( scattererType.TargetType );
			foreach ( var prop in scattererType.Properties )
			{
				if ( !prop.HasAttribute<PropertyAttribute>() )
					continue;

				if ( root.TryGetProperty( prop.Name, out var propElement ) )
				{
					var propValue = JsonSerializer.Deserialize( propElement.GetRawText(), prop.PropertyType );
					prop.SetValue( instance, propValue );
				}
			}

			return instance;
		}
		catch ( Exception e )
		{
			Log.Error( e, $"Failed to deserialize scatterer of type '{typeName}'" );
			return new SimpleScatterer();
		}
	}

	public static void JsonWrite( object value, Utf8JsonWriter writer )
	{
		if ( value is not Scatterer scatterer )
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStartObject();
		writer.WriteString( "$type", scatterer.GetType().Name );

		var typeDesc = TypeLibrary.GetType( scatterer.GetType() );
		if ( typeDesc is not null )
		{
			foreach ( var prop in typeDesc.Properties )
			{
				if ( !prop.HasAttribute<PropertyAttribute>() )
					continue;

				var propValue = prop.GetValue( scatterer );
				writer.WritePropertyName( prop.Name );
				JsonSerializer.Serialize( writer, propValue, propValue?.GetType() ?? typeof( object ) );
			}
		}

		writer.WriteEndObject();
	}
}
