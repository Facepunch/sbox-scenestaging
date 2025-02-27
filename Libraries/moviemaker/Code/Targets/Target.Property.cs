using System;

namespace Sandbox.MovieMaker;

#nullable enable

public static partial class Target
{
	public static ITarget Member( ITarget? parent, string memberName, Type? expectedType )
	{
		if ( parent is null ) return Unknown( parent, memberName, expectedType );

		if ( IsAnimParam( parent, memberName ) )
		{
			return FromAnimParam( parent, memberName, expectedType );
		}

		if ( IsMorph( parent, memberName ) )
		{
			return FromMorph( parent, memberName );
		}

		if ( TypeLibrary.GetType( parent.TargetType ) is not { } typeDesc )
		{
			return Unknown( parent, memberName, expectedType );
		}

		var member = typeDesc.Members
			.Where( x => x is FieldDescription or PropertyDescription )
			.FirstOrDefault( m => m.Name == memberName );

		var memberType = member switch
		{
			PropertyDescription propDesc => propDesc.PropertyType,
			FieldDescription fieldDesc => fieldDesc.FieldType,
			_ => null
		};

		if ( memberType is null )
		{
			return Unknown( parent, memberName, expectedType );
		}

		try
		{
			var propType = TypeLibrary.GetType( typeof(MemberMovieProperty<>) ).MakeGenericType( [memberType] );

			return TypeLibrary.Create<IProperty>( propType, [parent, member] );
		}
		catch
		{
			return Unknown( parent, memberName, expectedType );
		}
	}

	public static ITarget Unknown( ITarget? parent, string memberName, Type? expectedType )
	{
		return new UnknownTarget( parent, memberName, expectedType ?? typeof( object ) );
	}
}

/// <summary>
/// Movie property that references a field or property contained in another <see cref="ITarget"/>.
/// For example, a property in a <see cref="Component"/>.
/// </summary>
/// <typeparam name="T">Value type stored in the property.</typeparam>
file sealed class MemberMovieProperty<T>( ITarget parent, MemberDescription member )
	: IProperty<T>
{
	public ITarget Parent => parent;

	public bool IsBound => parent.IsBound;
	public bool CanWrite { get; } = member switch
	{
		PropertyDescription propDesc => propDesc.CanWrite,
		FieldDescription fieldDesc => !fieldDesc.IsInitOnly,
		_ => false
	};

	public T Value
	{
		// TODO: we can avoid boxing / reflection here when we're in engine code using System.Linq.Expressions

		get => parent.Value is { } target ? member switch
		{
			PropertyDescription propDesc => (T)propDesc.GetValue( target ),
			FieldDescription fieldDesc => (T)fieldDesc.GetValue( target ),
			_ => throw new NotImplementedException()
		} : default!;

		set
		{
			if ( parent.Value is not { } target )
			{
				return;
			}

			if ( !CanWrite ) return;

			SetInternal( target, value );

			if ( parent is IProperty { TargetType.IsValueType: true } parentMember )
			{
				parentMember.Value = target;
			}
		}
	}

	private void SetInternal( object target, object? value )
	{
		switch ( member )
		{
			case PropertyDescription propDesc:
				propDesc.SetValue( target, value );
				return;

			case FieldDescription fieldDesc:
				fieldDesc.SetValue( target, value );
				return;

			default:
				throw new NotImplementedException();
		}
	}

	string ITarget.Name => member.Name;
	Type ITarget.TargetType => typeof( T );

	object? IProperty.Value
	{
		get => Value;
		set => Value = (T)value!;
	}

	object? ITarget.Value => Value;
}

file sealed record UnknownTarget( ITarget? Parent, string Name, Type TargetType ) : ITarget
{
	public bool IsBound => false;
	public object? Value => null;
}
