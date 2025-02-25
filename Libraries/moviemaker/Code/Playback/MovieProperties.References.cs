using System;
using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Sandbox.MovieMaker;

#nullable enable

partial class MovieProperties : IJsonPopulator
{
	private readonly Dictionary<Guid, GameObject?> _gameObjectMap = new();
	private readonly Dictionary<Guid, Component?> _componentMap = new();

	/// <summary>
	/// Map a given track to reference a <paramref name="gameObject"/>. This mapping will be serialized with this instance.
	/// </summary>
	internal void SetReference( Guid trackId, GameObject? gameObject )
	{
		_gameObjectMap[trackId] = gameObject;
	}

	/// <summary>
	/// Map a given track to reference a <paramref name="component"/>. This mapping will be serialized with this instance.
	/// </summary>
	internal void SetReference( Guid trackId, Component? component )
	{
		_componentMap[trackId] = component;
	}

	private IGameObjectReferenceProperty CreateReferenceProperty( Guid trackId, string gameObjectName, IGameObjectReferenceProperty? parent = null ) =>
		new GameObjectMovieProperty( this, trackId, parent, gameObjectName );

	private IComponentReferenceProperty CreateReferenceProperty( Guid trackId, Type componentType, IGameObjectReferenceProperty parent ) =>
		new ComponentMovieProperty( this, trackId, parent, componentType );

	private record struct Model( ImmutableArray<MappingModel>? GameObjects = null, ImmutableArray<MappingModel>? Components = null );
	private record struct MappingModel( Guid Track, Guid Reference );

	public JsonNode Serialize()
	{
		// TODO: prune mappings if there aren't any matching tracks on any clip in the project?

		var model = new Model(
			_gameObjectMap
				.Where( x => x.Value.IsValid() )
				.Select( x => new MappingModel( x.Key, x.Value!.Id ) )
				.ToImmutableArray(),
			_componentMap
				.Where( x => x.Value.IsValid() )
				.Select( x => new MappingModel( x.Key, x.Value!.Id ) )
				.ToImmutableArray() );

		return Json.ToNode( model );
	}

	public void Deserialize( JsonNode? node )
	{
		_gameObjectMap.Clear();
		_componentMap.Clear();

		if ( Json.FromNode<Model?>( node ) is not { } model ) return;

		foreach ( var mapping in model.GameObjects ?? [] )
		{
			if ( scene.Directory.FindByGuid( mapping.Reference ) is { } gameObject )
			{
				_gameObjectMap[mapping.Track] = gameObject;
			}
		}

		foreach ( var mapping in model.Components ?? [] )
		{
			if ( scene.Directory.FindComponentByGuid( mapping.Reference ) is { } component )
			{
				_componentMap[mapping.Track] = component;
			}
		}
	}
}

/// <summary>
/// Movie property that references a <see cref="GameObject"/> in a scene.
/// </summary>
file sealed class GameObjectMovieProperty( MovieProperties properties, Guid trackId, IGameObjectReferenceProperty? parent, string name ) : IGameObjectReferenceProperty
{
	private GameObject? _value;

	public string PropertyName => Value?.Name ?? name;

	public bool IsBound => Value.IsValid();

	public IGameObjectReferenceProperty? Parent => parent;

	public GameObject? Value
	{
		get => _value.IsValid() ? _value : _value = AttemptAutoBind();
		set => properties.SetReference( trackId, _value = value );
	}

	/// <summary>
	/// If our parent object is bound, try to bind to a child object with a matching name.
	/// </summary>
	private GameObject? AttemptAutoBind()
	{
		return Parent is { Value: { } go }
			? go.Children.FirstOrDefault( x => x.Name == name )
			: null;
	}

	Type IMovieProperty.PropertyType => typeof( GameObject );
	object? IMovieProperty.Value => Value;
}

/// <summary>
/// Movie property that references a <see cref="Component"/> in a scene.
/// </summary>
file sealed class ComponentMovieProperty( MovieProperties properties, Guid trackId, IGameObjectReferenceProperty parent, Type componentType )
	: IComponentReferenceProperty
{
	private Component? _value;

	public string PropertyName { get; } = componentType.Name;

	public bool IsBound => Value.IsValid();

	public IGameObjectReferenceProperty Parent => parent;

	public Component? Value
	{
		get => _value.IsValid() ? _value : _value = AttemptAutoBind();
		set
		{
			if ( _value == value ) return;
			if ( value is not null && !componentType.IsInstanceOfType( value ) )
			{
				throw new ArgumentException( $"Expected a {componentType.Name} instance.", nameof( value ) );
			}

			properties.SetReference( trackId, _value = value );
		}
	}

	/// <summary>
	/// If our parent object is bound, try to bind to a component with the matching type.
	/// </summary>
	private Component? AttemptAutoBind()
	{
		return parent is { Value: { } go }
			? go.Components.Get( componentType, FindMode.EverythingInSelf )
			: null;
	}

	Type IMovieProperty.PropertyType => componentType;
	object? IMovieProperty.Value => Value;
}
