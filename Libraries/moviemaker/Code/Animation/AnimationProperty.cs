using System;

namespace Sandbox.Animation;

#nullable enable

/// <summary>
/// A property somewhere in a scene that is being controlled by an <see cref="AnimationTrack"/>.
/// </summary>
internal interface IAnimationProperty
{
	/// <summary>
	/// Pretty name of this property.
	/// </summary>
	string DisplayName { get; }

	/// <summary>
	/// Property value type.
	/// </summary>
	Type PropertyType { get; }

	/// <summary>
	/// Gets the value of this property, it must match <see cref="PropertyType"/>.
	/// </summary>
	object? GetValue();

	/// <summary>
	/// Sets the value of this property, it must match <see cref="PropertyType"/>.
	/// </summary>
	void SetValue( object? value );

	/// <summary>
	/// Get a serializable reference to this property.
	/// </summary>
	AnimationPropertyReference ToReference();
}

internal static class AnimationProperty
{
	public static IAnimationProperty Create( object parent, string name )
	{
		var typeDesc = TypeLibrary.GetType( parent.GetType() );
		var member = typeDesc.Members
			.Where( x => x.Name == name )
			.FirstOrDefault( x => x is PropertyDescription or FieldDescription )
			?? throw new Exception( $"Unable to find property '{name}' in type '{typeDesc.Name}'." );

		return new ReflectionAnimationProperty( parent, member );
	}
}

file class ReflectionAnimationProperty : IAnimationProperty
{
	private string? _parentName;

	private object Parent { get; }
	private MemberDescription Member { get; }

	public string DisplayName => $"{ParentName} → {Member.Title}";

	public string Name => Member.Name;
	public Type PropertyType { get; }

	public ReflectionAnimationProperty( object parent, MemberDescription member )
	{
		Parent = parent;
		Member = member;

		PropertyType = member switch
		{
			PropertyDescription propDesc => propDesc.PropertyType,
			FieldDescription fieldDesc => fieldDesc.FieldType,
			_ => throw new NotImplementedException()
		};
	}

	// TODO: System.Linq.Expressions fast path when we're in engine code

	public object GetValue() => Member switch
	{
		PropertyDescription propDesc => propDesc.GetValue( Parent ),
		FieldDescription fieldDesc => fieldDesc.GetValue( Parent ),
		_ => throw new NotImplementedException()
	};

	public void SetValue( object? value )
	{
		switch ( Member )
		{
			case PropertyDescription propDesc:
				propDesc.SetValue( Parent, value );
				break;
			case FieldDescription fieldDesc:
				fieldDesc.SetValue( Parent, value );
				break;
			default:
				throw new NotImplementedException();
		}
	}

	public AnimationPropertyReference ToReference() => Parent switch
	{
		GameObject go => AnimationPropertyReference.FromGameObject( go, Name ),
		Component comp => AnimationPropertyReference.FromComponent( comp, Name ),
		_ => throw new NotImplementedException()
	};

	private string ParentName => _parentName ??= Parent switch
	{
		GameObject go => go.Name,
		Component comp => $"{comp.GameObject.Name} → {TypeLibrary.GetType( comp.GetType() ).Title}",
		_ => Parent?.ToString() ?? "null"
	};
}
