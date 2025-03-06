using System;

namespace Sandbox.MovieMaker.Properties;

#nullable enable

/// <summary>
/// Movie property that references a field or property contained in another <see cref="ITrackTarget"/>.
/// For example, a property in a <see cref="Component"/>.
/// </summary>
/// <typeparam name="T">Value type stored in the property.</typeparam>
file sealed record MemberProperty<T>( ITrackTarget Parent, MemberDescription Member )
	: ITrackProperty<T>
{
	public string Name => Member.Name;

	public bool CanWrite => Member switch
	{
		PropertyDescription propDesc => propDesc.CanWrite,
		FieldDescription fieldDesc => !fieldDesc.IsInitOnly,
		_ => false
	};

	public T Value
	{
		// TODO: we can avoid boxing / reflection here when we're in engine code using System.Linq.Expressions

		get => Parent.Value is { } target ? Member switch
		{
			PropertyDescription propDesc => (T)propDesc.GetValue( target ),
			FieldDescription fieldDesc => (T)fieldDesc.GetValue( target ),
			_ => throw new NotImplementedException()
		} : default!;

		set
		{
			if ( Parent.Value is not { } target )
			{
				return;
			}

			if ( !CanWrite ) return;

			SetInternal( target, value );

			if ( Parent is ITrackProperty { TargetType.IsValueType: true } parentMember )
			{
				parentMember.Value = target;
			}
		}
	}

	private void SetInternal( object target, object? value )
	{
		switch ( Member )
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
}

file sealed class MemberPropertyFactory : ITrackPropertyFactory
{
	int ITrackPropertyFactory.Order => 0x4000_0000;

	private MemberDescription? GetMember( ITrackTarget parent, string name )
	{
		if ( TypeLibrary.GetType( parent.TargetType ) is not { } typeDesc ) return null;

		return typeDesc.Members
			.Where( x => x is FieldDescription or PropertyDescription )
			.FirstOrDefault( m => m.Name == name );
	}

	public Type? GetTargetType( ITrackTarget parent, string name )
	{
		return GetMember( parent, name ) switch
		{
			PropertyDescription propDesc => propDesc.PropertyType,
			FieldDescription fieldDesc => fieldDesc.FieldType,
			_ => null
		};
	}

	public ITrackProperty<T> CreateProperty<T>( ITrackTarget parent, string name )
	{
		return new MemberProperty<T>( parent, GetMember( parent, name )! );
	}
}
