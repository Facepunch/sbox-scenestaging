using System;

namespace Sandbox.MovieMaker;

#nullable enable

internal static partial class TrackTarget
{
	public static ITrackTarget FromMember( ITrackTarget? parent, string memberName, Type? expectedType )
	{
		if ( parent is null ) return FromUnknown( parent, memberName, expectedType );

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
			return FromUnknown( parent, memberName, expectedType );
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
			return FromUnknown( parent, memberName, expectedType );
		}

		try
		{
			var propType = TypeLibrary.GetType( typeof(MemberMovieProperty<>) ).MakeGenericType( [memberType] );

			return TypeLibrary.Create<IMember>( propType, [parent, member] );
		}
		catch
		{
			return FromUnknown( parent, memberName, expectedType );
		}
	}

	private static ITrackTarget FromUnknown( ITrackTarget? parent, string memberName, Type? expectedType )
	{
		return new UnknownTarget( parent, memberName, expectedType ?? typeof( object ) );
	}
}

/// <summary>
/// Movie property that references a field or property contained in another <see cref="ITrackTarget"/>.
/// For example, a property in a <see cref="Component"/>.
/// </summary>
/// <typeparam name="T">Value type stored in the property.</typeparam>
file sealed class MemberMovieProperty<T>( ITrackTarget parent, MemberDescription member )
	: IMember<T>
{
	public ITrackTarget Parent => parent;

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

			if ( parent is IMember { TargetType.IsValueType: true } parentMember )
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

	string ITrackTarget.Name => member.Name;
	Type ITrackTarget.TargetType => typeof( T );

	object? IMember.Value
	{
		get => Value;
		set => Value = (T)value!;
	}

	object? ITrackTarget.Value => Value;
}

file sealed record UnknownTarget( ITrackTarget? Parent, string Name, Type TargetType ) : ITrackTarget
{
	public bool IsBound => false;
	public object? Value => null;
}
