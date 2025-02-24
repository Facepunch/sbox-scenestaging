using System;

namespace Sandbox.MovieMaker;

#nullable enable

internal static partial class MovieProperty
{
	public static IMemberProperty FromMember( IMovieProperty parent, string memberName, Type? expectedType )
	{
		if ( IsAnimParam( parent, memberName ) )
		{
			return FromAnimParam( parent, memberName, expectedType );
		}

		if ( IsMorph( parent, memberName ) )
		{
			return FromMorph( parent, memberName );
		}

		if ( TypeLibrary.GetType( parent.PropertyType ) is not { } typeDesc )
		{
			throw new ArgumentException( $"Unable to access type '{parent.PropertyType.Name}'.", nameof(parent) );
		}

		var member = typeDesc.Members
			.Where( x => x is FieldDescription or PropertyDescription )
			.FirstOrDefault( m => m.Name == memberName );

		var memberType = member switch
		{
			PropertyDescription propDesc => propDesc.PropertyType,
			FieldDescription fieldDesc => fieldDesc.FieldType,
			_ => throw new ArgumentException( $"Unable to find property or field '{memberName}' in type '{parent.PropertyType.Name}'.", nameof(memberName) )
		};

		var propType = TypeLibrary.GetType( typeof( MemberMovieProperty<> ) ).MakeGenericType( [memberType] );

		return TypeLibrary.Create<IMemberProperty>( propType, [parent, member] );
	}
}

/// <summary>
/// Movie property that references a field or property contained in another <see cref="IMovieProperty"/>.
/// For example, a property in a <see cref="Component"/>.
/// </summary>
/// <typeparam name="T">Value type stored in the property.</typeparam>
file sealed class MemberMovieProperty<T>( IMovieProperty parent, MemberDescription member )
	: IMemberProperty<T>
{
	public IMovieProperty Parent => parent;

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

			if ( parent is IMemberProperty { PropertyType.IsValueType: true } parentMember )
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

	string IMovieProperty.PropertyName => member.Name;
	Type IMovieProperty.PropertyType => typeof( T );

	object? IMemberProperty.Value
	{
		get => Value;
		set => Value = (T)value!;
	}

	object? IMovieProperty.Value => Value;
}
