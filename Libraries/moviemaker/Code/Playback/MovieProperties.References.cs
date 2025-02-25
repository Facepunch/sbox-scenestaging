using System;
using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Sandbox.MovieMaker;

#nullable enable

// When MovieProperties gets serialized, it writes tracks that map to GameObject or Component references.
// Here we store those mappings, and handle creating IMovieProperty instances that access them.

partial class MovieProperties : IJsonPopulator
{
	private readonly Dictionary<Guid, GameObject?> _gameObjectMap = new();
	private readonly Dictionary<Guid, Component?> _componentMap = new();

	private IGameObjectReference CreateReferenceProperty( Guid trackId, string gameObjectName,
		IGameObjectReference? parent = null ) =>
		new GameObjectMovieProperty( this, trackId, parent, gameObjectName );

	private IComponentReference CreateReferenceProperty( Guid trackId, Type componentType,
		IGameObjectReference parent ) =>
		new ComponentMovieProperty( this, trackId, parent, componentType );

	private record struct Model(
		ImmutableArray<MappingModel>? GameObjects = null,
		ImmutableArray<MappingModel>? Components = null );

	private record struct MappingModel( Guid Track, Guid Reference );

	JsonNode IJsonPopulator.Serialize()
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

	void IJsonPopulator.Deserialize( JsonNode? node )
	{
		_gameObjectMap.Clear();
		_componentMap.Clear();

		if ( Json.FromNode<Model?>( node ) is not { } model ) return;

		// Load GameObject / Component mappings, updating any matching movie properties if they exist

		foreach ( var mapping in model.GameObjects ?? [] )
		{
			if ( scene.Directory.FindByGuid( mapping.Reference ) is not { } gameObject ) continue;

			if ( _properties!.GetValueOrDefault( mapping.Track ) is GameObjectMovieProperty property )
			{
				property.Value = gameObject;
			}
			else
			{
				_gameObjectMap[mapping.Track] = gameObject;
			}
		}

		foreach ( var mapping in model.Components ?? [] )
		{
			if ( scene.Directory.FindComponentByGuid( mapping.Reference ) is not { } component ) continue;

			if ( _properties!.GetValueOrDefault( mapping.Track ) is ComponentMovieProperty property )
			{
				property.Value = component;
			}
			else
			{
				_componentMap[mapping.Track] = component;
			}
		}
	}

	/// <summary>
	/// Movie property that references a <see cref="GameObject"/> in a scene.
	/// </summary>
	private sealed class GameObjectMovieProperty(
		MovieProperties properties,
		Guid trackId,
		IGameObjectReference? parent,
		string name ) : IGameObjectReference
	{
		private GameObject? _value = properties._gameObjectMap.GetValueOrDefault( trackId );

		public string PropertyName => Value?.Name ?? name;

		public bool IsBound => Value.IsValid();

		public IGameObjectReference? Parent => parent;

		public GameObject? Value
		{
			get => _value.IsValid() ? _value : _value = AttemptAutoBind();
			set => properties._gameObjectMap[trackId] = _value = value;
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

		Type IMovieProperty.PropertyType => typeof(GameObject);
		object? IMovieProperty.Value => Value;
	}

	/// <summary>
	/// Movie property that references a <see cref="Component"/> in a scene.
	/// </summary>
	private sealed class ComponentMovieProperty(
		MovieProperties properties,
		Guid trackId,
		IGameObjectReference parent,
		Type componentType )
		: IComponentReference
	{
		private Component? _value = properties._componentMap.GetValueOrDefault( trackId );

		public string PropertyName { get; } = componentType.Name;

		public bool IsBound => Value.IsValid();

		public IGameObjectReference Parent => parent;

		public Component? Value
		{
			get => _value.IsValid() ? _value : _value = AttemptAutoBind();
			set
			{
				if ( _value == value ) return;
				if ( value is not null && !componentType.IsInstanceOfType( value ) )
				{
					throw new ArgumentException( $"Expected a {componentType.Name} instance.", nameof(value) );
				}

				properties._componentMap[trackId] = _value = value;
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
}
