using System;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A property somewhere in a scene that is being controlled by a <see cref="MovieTrack"/>.
/// </summary>
public interface IMovieProperty
{
	string PropertyName { get; }
	Type PropertyType { get; }

	bool IsBound { get; }

	object? Value { get; set; }
}

/// <summary>
/// Typed <see cref="IMovieProperty"/>.
/// </summary>
/// <typeparam name="T">Value type stored in the property.</typeparam>
internal interface IMovieProperty<T> : IMovieProperty
{
	new T Value { get; set; }
}

/// <summary>
/// A property referencing a <see cref="GameObject"/> or <see cref="Component"/> in the scene.
/// </summary>
public interface ISceneReferenceMovieProperty : IMovieProperty
{
	GameObject? GameObject { get; }
	Component? Component { get; }
}

/// <summary>
/// Movie property that represents a member inside another property.
/// </summary>
public interface IMemberMovieProperty : IMovieProperty
{
	/// <summary>
	/// Property that this member belongs to.
	/// </summary>
	IMovieProperty TargetProperty { get; }
}

internal static partial class MovieProperty
{
	public static ISceneReferenceMovieProperty FromGameObject( GameObject go )
	{
		return new GameObjectMovieProperty( go );
	}

	public static ISceneReferenceMovieProperty FromGameObject( string placeholderName )
	{
		return new GameObjectMovieProperty( placeholderName );
	}

	public static ISceneReferenceMovieProperty FromComponent( IMovieProperty target, Component comp )
	{
		return new ComponentMovieProperty( target, comp );
	}

	public static ISceneReferenceMovieProperty FromComponentType( IMovieProperty target, Type type )
	{
		return new ComponentMovieProperty( target, type );
	}

	public static IMemberMovieProperty FromMember( IMovieProperty target, string memberName, Type? expectedType )
	{
		if ( FromAnimParam( target, memberName, expectedType ) is { } animParamProperty )
		{
			return animParamProperty;
		}

		var targetType = TypeLibrary.GetType( target.PropertyType );
		var member = targetType.Members
			.Where( x => x is FieldDescription or PropertyDescription )
			.FirstOrDefault( m => m.Name == memberName );

		var memberType = member switch
		{
			PropertyDescription propDesc => propDesc.PropertyType,
			FieldDescription fieldDesc => fieldDesc.FieldType,
			_ => throw new ArgumentException(
				$"Unable to find property or field '{memberName}' in type '{targetType.Name}'.", nameof(memberName) )
		};

		var propType = TypeLibrary.GetType( typeof(MemberMovieProperty<>) ).MakeGenericType( [memberType] );

		return TypeLibrary.Create<IMemberMovieProperty>( propType, [target, member] );
	}
}

/// <summary>
/// Movie property that references a <see cref="GameObject"/> in a scene.
/// </summary>
file sealed class GameObjectMovieProperty : IMovieProperty<GameObject?>, ISceneReferenceMovieProperty
{
	private string _placeholderName;

	public string PropertyName => Value?.Name ?? _placeholderName;
	public Type PropertyType => typeof(GameObject);

	public bool IsBound => Value.IsValid();

	public GameObject? Value { get; set; }

	public GameObjectMovieProperty( GameObject value )
		: this ( value.Name )
	{
		Value = value;
	}

	public GameObjectMovieProperty( string placeholderName )
	{
		_placeholderName = placeholderName;
	}

	object? IMovieProperty.Value
	{
		get => Value;
		set => Value = (GameObject?)value;
	}

	GameObject? ISceneReferenceMovieProperty.GameObject => Value;
	Component? ISceneReferenceMovieProperty.Component => null;
}

/// <summary>
/// Movie property that references a <see cref="Component"/> in a scene.
/// </summary>
/// <typeparam name="T">Component type stored in the property.</typeparam>
file sealed class ComponentMovieProperty : IMovieProperty<Component?>, ISceneReferenceMovieProperty, IMemberMovieProperty
{
	private Component? _value;

	public string PropertyName { get; }
	public Type PropertyType { get; }

	public bool IsBound => Value.IsValid();

	public Component? Value
	{
		get => _value ??= AttemptAutoResolve();
		set => _value = value;
	}

	public IMovieProperty TargetProperty { get; }

	public ComponentMovieProperty( IMovieProperty targetObjectProperty, Component value )
		: this( targetObjectProperty, value.GetType() )
	{
		Value = value;
	}

	public ComponentMovieProperty( IMovieProperty targetObjectProperty, Type componentType )
	{
		TargetProperty = targetObjectProperty;

		PropertyType = componentType;
		PropertyName = componentType.Name;
	}

	object? IMovieProperty.Value
	{
		get => Value;
		set => Value = (Component?)value;
	}

	GameObject? ISceneReferenceMovieProperty.GameObject => Value?.GameObject;
	Component? ISceneReferenceMovieProperty.Component => Value;

	private Component? AttemptAutoResolve()
	{
		return TargetProperty is not { IsBound: true, Value: GameObject go }
			? null
			: go.Components.Get( PropertyType, FindMode.EverythingInSelf );
	}
}

/// <summary>
/// Movie property that references a field or property contained in another <see cref="IMovieProperty"/>.
/// For example, a property in a <see cref="Component"/>.
/// </summary>
/// <typeparam name="T">Value type stored in the property.</typeparam>
file sealed class MemberMovieProperty<T> : IMovieProperty<T>, IMemberMovieProperty
{
	public IMovieProperty TargetProperty { get; }
	public MemberDescription Member { get; }

	public bool IsBound => TargetProperty.IsBound;

	public T Value
	{
		// TODO: we can avoid boxing / reflection here when we're in engine code using System.Linq.Expressions

		get => Member switch
		{
			PropertyDescription propDesc => (T)propDesc.GetValue( TargetProperty.Value ),
			FieldDescription fieldDesc => (T)fieldDesc.GetValue( TargetProperty.Value ),
			_ => throw new NotImplementedException()
		};

		set
		{
			switch ( Member )
			{
				case PropertyDescription propDesc:
					propDesc.SetValue( TargetProperty.Value, value );
					return;

				case FieldDescription fieldDesc:
					fieldDesc.SetValue( TargetProperty.Value, value );
					return;

				default:
					throw new NotImplementedException();
			}
		}
	}

	public MemberMovieProperty( IMovieProperty targetProperty, MemberDescription member )
	{
		TargetProperty = targetProperty;
		Member = member;
	}

	string IMovieProperty.PropertyName => Member.Name;
	Type IMovieProperty.PropertyType => typeof(T);

	object? IMovieProperty.Value
	{
		get => Value;
		set => Value = (T)value!;
	}
}
