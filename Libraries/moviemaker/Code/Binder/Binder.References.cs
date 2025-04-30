using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

// When MovieProperties get serialized, we write track mappings to GameObject or Component references.
// Here we store those mappings, and handle creating ITarget instances that access them.

[JsonConverter( typeof(BinderConverter) )]
partial class TrackBinder : IJsonPopulator
{
	private readonly Dictionary<Guid, GameObject?> _gameObjectMap = new();
	private readonly Dictionary<Guid, Component?> _componentMap = new();

	/// <summary>
	/// Finds track IDs currently explicitly bound to the given <paramref name="gameObject"/>.
	/// </summary>
	public IEnumerable<Guid> GetTrackIds( GameObject gameObject ) => _gameObjectMap
		.Where( x => x.Value == gameObject )
		.Select( x => x.Key );

	/// <summary>
	/// Finds track IDs currently explicitly bound to the given <paramref name="component"/>.
	/// </summary>
	public IEnumerable<Guid> GetTrackIds( Component component ) => _componentMap
		.Where( x => x.Value == component )
		.Select( x => x.Key );

	#region Serialization

	private record struct Model(
		ImmutableArray<MappingModel>? GameObjects = null,
		ImmutableArray<MappingModel>? Components = null );

	private record struct MappingModel( Guid Track, Guid? Reference );

	public JsonNode Serialize()
	{
		// TODO: prune mappings if there aren't any matching tracks on any clip in the project?

		var model = new Model(
			_gameObjectMap
				.Select( x => new MappingModel( x.Key, x.Value.IsValid() ? x.Value.Id : null ) )
				.ToImmutableArray(),
			_componentMap
				.Select( x => new MappingModel( x.Key, x.Value.IsValid() ? x.Value.Id : null ) )
				.ToImmutableArray() );

		return Json.ToNode( model );
	}

	public void Deserialize( JsonNode? node )
	{
		_gameObjectMap.Clear();
		_componentMap.Clear();

		if ( Json.FromNode<Model?>( node ) is not { } model ) return;

		if ( model.GameObjects is { } objects )
		{
			foreach ( var mapping in objects )
			{
				_gameObjectMap[mapping.Track] = mapping.Reference is { } id && Scene.Directory.FindByGuid( id ) is { } gameObject
					? gameObject : null;
			}
		}

		if ( model.Components is { } components )
		{
			foreach ( var mapping in components )
			{
				_componentMap[mapping.Track] = mapping.Reference is { } id && Scene.Directory.FindComponentByGuid( id ) is { } component
					? component
					: null;
			}
		}
	}

	#endregion

	#region Reference Targets

	private abstract class Reference<T>( ITrackReference<GameObject>? parent, Guid id ) : ITrackReference<T>
		where T : class, IValid
	{
		private T? _autoBound;

		public Guid Id => id;
		public abstract string Name { get; }
		public virtual Type TargetType => typeof(T);
		public abstract bool IsActive { get; }
		public ITrackReference<GameObject>? Parent => parent;

		public T? Value
		{
			get
			{
				if ( TryGetBinding( out var bound ) ) return bound;

				return _autoBound.IsValid() ? _autoBound : _autoBound ??= AutoBind();
			}
		}

		public void Reset()
		{
			OnReset();
			_autoBound = null;
		}

		public abstract void Bind( T? value );

		protected abstract T? AutoBind();

		protected abstract bool TryGetBinding( out T? value );
		protected abstract void OnReset();
	}

	/// <summary>
	/// Target that references a <see cref="GameObject"/> in a scene.
	/// </summary>
	private sealed class GameObjectReference( ITrackReference<GameObject>? parent, string name, TrackBinder binder, Guid id, Guid? referenceId )
		: Reference<GameObject>( parent, id ), ITrackReference<GameObject>
	{
		public override string Name => Value?.Name ?? name;
		public override bool IsActive => Value is { IsValid: true, Active: true };

		public override void Bind( GameObject? value ) => binder._gameObjectMap[Id] = value;

		/// <summary>
		/// If our parent object is bound, try to bind to a child object with a matching name.
		/// If we have no parent, look up by referenceId, or default to a root object with the right name.
		/// </summary>
		protected override GameObject? AutoBind()
		{
			if ( Parent is null )
			{
				if ( referenceId is { } refId && binder.Scene.Directory.FindByGuid( refId ) is { } match )
				{
					return match;
				}

				return binder.Scene.Children.FirstOrDefault( x => x.Name == name );
			}

			return Parent?.Value is { } go
				? go.Children.FirstOrDefault( x => x.Name == name )
				: null;
		}

		protected override bool TryGetBinding( out GameObject? value ) => binder._gameObjectMap.TryGetValue( Id, out value );
		protected override void OnReset() => binder._gameObjectMap.Remove( Id );
	}

	private TypeDescription? _componentReferenceType;

	private ITrackReference CreateComponentReference( ITrackReference<GameObject>? parent, Type componentType, TrackBinder binder, Guid id )
	{
		_componentReferenceType ??= TypeLibrary.GetType( typeof(ComponentReference<>) );

		return _componentReferenceType.CreateGeneric<ITrackReference>( [componentType], [parent, binder, id] );
	}

	/// <summary>
	/// Target that references a <see cref="Component"/> in a scene.
	/// </summary>
	private sealed class ComponentReference<T>( ITrackReference<GameObject>? parent, TrackBinder binder, Guid id )
		: Reference<T>( parent, id ) where T : Component
	{
		public override string Name => typeof(T).Name;
		public override Type TargetType => typeof(T);
		public override bool IsActive => Value is { IsValid: true, Active: true };

		public override void Bind( T? value ) => binder._componentMap[Id] = value;

		/// <summary>
		/// If our parent object is bound, try to bind to a component with a matching type.
		/// </summary>
		protected override T? AutoBind()
		{
			return Parent?.Value is { } go
				? go.Components.Get<T>( FindMode.EverythingInSelf )
				: null;
		}

		protected override bool TryGetBinding( out T? value )
		{
			if ( binder._componentMap.TryGetValue( Id, out var binding ) )
			{
				value = binding as T;
				return true;
			}

			value = default;
			return false;
		}

		protected override void OnReset() => binder._componentMap.Remove( Id );
	}

	#endregion
}

file sealed class BinderConverter : JsonConverter<TrackBinder>
{
	public override TrackBinder Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		var node = JsonSerializer.Deserialize<JsonNode>( ref reader, options );
		var binder = new TrackBinder( Game.ActiveScene );

		binder.Deserialize( node );

		return binder;
	}

	public override void Write( Utf8JsonWriter writer, TrackBinder value, JsonSerializerOptions options )
	{
		JsonSerializer.Serialize( writer, value.Serialize(), options );
	}
}
