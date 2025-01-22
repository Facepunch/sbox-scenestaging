using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.Animation;

#nullable enable

/// <summary>
/// Describes the location of a property in a scene in a way that we can serialize.
/// </summary>
internal readonly struct AnimationPropertyReference
{
	public static AnimationPropertyReference FromGameObject( GameObject go, string propertyName ) =>
		new( Json.ToNode( go, typeof(GameObject) ), propertyName );

	public static AnimationPropertyReference FromComponent( Component comp, string propertyName ) =>
		new( Json.ToNode( comp, typeof(Component) ), propertyName );

	/// <summary>
	/// References the <see cref="GameObject"/> or <see cref="Component"/> that contains this property.
	/// </summary>
	[JsonPropertyName( "Source" )]
	private JsonNode SourceModel { get; }

	/// <summary>
	/// Name of the property.
	/// </summary>
	[JsonPropertyName( "Property" )]
	public string PropertyName { get; }

	[JsonConstructor]
	private AnimationPropertyReference( JsonNode sourceModel, string propertyName )
	{
		SourceModel = sourceModel;
		PropertyName = propertyName;
	}

	private object? ResolveSource()
	{
		if ( SourceModel is not JsonObject sourceObj ) return null;

		// TODO: use GameObjectReference / ComponentReference directly when we're in engine code

		return sourceObj["_type"]?.GetValue<string>() switch
		{
			"gameobject" => Json.FromNode<GameObject>( sourceObj ),
			"component" => Json.FromNode<Component>( sourceObj ),
			_ => null
		};
	}

	/// <summary>
	/// Attempts to find the referenced property in the current scene.
	/// </summary>
	public IAnimationProperty? Resolve()
	{
		return ResolveSource() is { } source
			? AnimationProperty.Create( source, PropertyName )
			: null;
	}
}
