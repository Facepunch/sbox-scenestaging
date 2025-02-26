using System;
using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Sandbox.MovieMaker;

#nullable enable

// When MovieProperties get serialized, we write track mappings to GameObject or Component references.
// Here we store those mappings, and handle creating IMovieProperty instances that access them.

partial class MovieTargets : IJsonPopulator
{
	private readonly Dictionary<Guid, GameObject?> _gameObjectMap = new();
	private readonly Dictionary<Guid, Component?> _componentMap = new();

	public void SetReference( Guid trackId, GameObject? gameObject )
	{
		_gameObjectMap[trackId] = gameObject;

		UpdateReference( trackId, gameObject );
	}

	public void SetReference( Guid trackId, Component? component )
	{
		_componentMap[trackId] = component;

		UpdateReference( trackId, component );
	}

	public void SetReference( ITrackDescription track, GameObject? gameObject )
	{
		if ( track.TargetType != typeof(GameObject) )
		{
			throw new ArgumentException( $"Expected a {nameof(GameObject)} track.", nameof(track) );
		}

		Touch( track );
		SetReference( track.Id, gameObject );
	}

	public void SetReference( ITrackDescription track, Component? component )
	{
		if ( !track.TargetType.IsAssignableTo( typeof(Component) ) )
		{
			throw new ArgumentException( $"Expected a {nameof(Component)} track.", nameof(track) );
		}

		if ( component is not null && !track.TargetType.IsInstanceOfType( component ) )
		{
			throw new ArgumentException( $"Expected a {track.TargetType} instance.", nameof(component) );
		}

		Touch( track );
		SetReference( track.Id, component );
	}

	private void UpdateReference<T>( Guid trackId, T? value )
		where T : class, IValid
	{
		if ( !_targets.TryGetValue( trackId, out var property ) ) return;
		if ( property is not ReferenceProperty<T> goProperty ) return;

		goProperty.Value = value;
	}

	#region Serialization

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

		foreach ( var mapping in model.GameObjects ?? [] )
		{
			if ( scene.Directory.FindByGuid( mapping.Reference ) is not { } gameObject ) continue;

			SetReference( mapping.Track, gameObject );
		}

		foreach ( var mapping in model.Components ?? [] )
		{
			if ( scene.Directory.FindComponentByGuid( mapping.Reference ) is not { } component ) continue;

			SetReference( mapping.Track, component );
		}
	}

	#endregion

	#region Reference Properties

	/// <summary>
	/// Base class for properties that reference objects in a scene, rather than being members of other properties.
	/// These references get serialized in <see cref="MovieTargets"/> to make them persist. If they haven't got
	/// an explicit mapping in the scene, they can <see cref="AutoBind"/> if they have a bound parent property
	/// </summary>
	private abstract class ReferenceProperty<T>( IGameObjectReference? parent, T? value ) : ITrackTarget
		where T : class, IValid
	{
		private T? _value = value;

		public abstract string Name { get; }
		public virtual Type TargetType => typeof(T);
		public IGameObjectReference? Parent => parent;
		public bool IsBound => Value.IsValid();

		public T? Value
		{
			get => _value.IsValid() ? _value : _value = AutoBind();
			internal set => _value = value;
		}

		protected abstract T? AutoBind();

		ITrackTarget? ITrackTarget.Parent { get; }
		object? ITrackTarget.Value => Value;
	}

	/// <summary>
	/// Movie property that references a <see cref="GameObject"/> in a scene.
	/// </summary>
	private sealed class GameObjectReference( IGameObjectReference? parent, string name, GameObject? value )
		: ReferenceProperty<GameObject>( parent, value ), IGameObjectReference
	{
		public override string Name => Value?.Name ?? name;

		/// <summary>
		/// If our parent object is bound, try to bind to a child object with a matching name.
		/// </summary>
		protected override GameObject? AutoBind()
		{
			return Parent is { Value: { } go }
				? go.Children.FirstOrDefault( x => x.Name == name )
				: null;
		}
	}

	/// <summary>
	/// Movie property that references a <see cref="Component"/> in a scene.
	/// </summary>
	private sealed class ComponentReference( IGameObjectReference? parent, Type componentType, Component? value )
		: ReferenceProperty<Component>( parent, value ), IComponentReference
	{
		public override string Name => componentType.Name;
		public override Type TargetType => componentType;

		/// <summary>
		/// If our parent object is bound, try to bind to a component with a matching type.
		/// </summary>
		protected override Component? AutoBind()
		{
			return Parent is { Value: { } go }
				? go.Components.Get( componentType, FindMode.EverythingInSelf )
				: null;
		}
	}

	#endregion
}
