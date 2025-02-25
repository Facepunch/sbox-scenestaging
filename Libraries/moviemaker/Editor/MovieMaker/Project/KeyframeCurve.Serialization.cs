using System.Collections.Immutable;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Editor.MovieMaker;

[JsonConverter( typeof(KeyframeCurveConverter) )]
partial class KeyframeCurve<T>
{

}

file sealed class KeyframeCurveConverter : JsonConverterFactory
{
	public override bool CanConvert( Type typeToConvert )
	{
		return typeToConvert.IsConstructedGenericType && typeToConvert.GetGenericTypeDefinition() == typeof( KeyframeCurve<> );
	}

	public override JsonConverter CreateConverter( Type typeToConvert, JsonSerializerOptions options )
	{
		return (JsonConverter)Activator.CreateInstance( typeof( Converter<> ).MakeGenericType( typeToConvert.GetGenericArguments()[0] ) )!;
	}

	private class Converter<T> : JsonConverter<KeyframeCurve<T>>
	{
		private record Model( InterpolationMode Interpolation, IReadOnlyList<Keyframe<T>> Keyframes );

		public override KeyframeCurve<T> Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			var model = JsonSerializer.Deserialize<Model>( ref reader, options )!;
			var curve = new KeyframeCurve<T> { Interpolation = model.Interpolation };

			foreach ( var keyframe in model.Keyframes )
			{
				curve.SetKeyframe( keyframe );
			}

			return curve;
		}

		public override void Write( Utf8JsonWriter writer, KeyframeCurve<T> value, JsonSerializerOptions options )
		{
			var model = new Model( value.Interpolation, value.ToImmutableList<Keyframe<T>>() );

			JsonSerializer.Serialize( writer, model, options );
		}
	}
}
