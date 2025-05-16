using System;
using System.Collections.Immutable;
using System.Reflection;

namespace Sandbox.MovieMaker.Properties;

#nullable enable

/// <summary>
/// Used by <see cref="TrackBinder"/> to create <see cref="ITrackProperty"/> instances that allow <see cref="ITrack"/>s
/// to modify values in a scene.
/// </summary>
public interface ITrackPropertyFactory
{
	/// <summary>
	/// Used to sort the order that factories are considered when trying to create a property.
	/// </summary>
	public int Order => 0;

	/// <summary>
	/// Decides if this factory can create a property given a <paramref name="parent"/> target and <paramref name="name"/>.
	/// Returns any non-<see langword="null"/> type if this factory can create such a property, after which <see cref="CreateProperty{T}"/>
	/// will be called using that type.
	/// </summary>
	Type? GetTargetType( ITrackTarget parent, string name );

	/// <summary>
	/// Create a property with the given <paramref name="parent"/>, <paramref name="name"/>, and property value type <typeparamref name="T"/>.
	/// The target type was previously returned by <see cref="GetTargetType"/>, or read from a deserialized track.
	/// </summary>
	ITrackProperty<T> CreateProperty<T>( ITrackTarget parent, string name );
}

/// <summary>
/// An <see cref="ITrackPropertyFactory"/> that only creates properties nested inside a particular <typeparamref name="TParent"/>
/// target type.
/// </summary>
/// <typeparam name="TParent">Parent target type that this factory's properties are always nested inside.</typeparam>
public interface ITrackPropertyFactory<in TParent> : ITrackPropertyFactory
	where TParent : ITrackTarget
{
	/// <inheritdoc cref="ITrackPropertyFactory.GetTargetType"/>
	Type? GetTargetType( TParent parent, string name );

	/// <inheritdoc cref="ITrackPropertyFactory.CreateProperty{T}"/>
	ITrackProperty<T> CreateProperty<T>( TParent parent, string name );

	Type? ITrackPropertyFactory.GetTargetType( ITrackTarget parent, string name ) =>
		parent is TParent typedParent
			? GetTargetType( typedParent, name )
			: null;

	ITrackProperty<T> ITrackPropertyFactory.CreateProperty<T>( ITrackTarget parent, string name ) =>
		CreateProperty<T>( (TParent)parent, name );
}

/// <summary>
/// An <see cref="ITrackPropertyFactory"/> that only creates properties nested inside a particular <typeparamref name="TParent"/>
/// target type, and that always have the same property value type <typeparamref name="TValue"/>.
/// </summary>
/// <typeparam name="TParent">Parent target type that this factory's properties are always nested inside.</typeparam>
/// <typeparam name="TValue">Property value type for properties created by this factory.</typeparam>
public interface ITrackPropertyFactory<in TParent, TValue> : ITrackPropertyFactory<TParent>
	where TParent : ITrackTarget
{
	/// <summary>
	/// Returns true if this factory can create a property with the given <paramref name="parent"/> and <paramref name="name"/>.
	/// </summary>
	bool PropertyExists( TParent parent, string name );

	/// <summary>
	/// Creates a property with the given <paramref name="parent"/> and <paramref name="name"/>.
	/// </summary>
	ITrackProperty<TValue> CreateProperty( TParent parent, string name );

	Type? ITrackPropertyFactory<TParent>.GetTargetType( TParent parent, string name ) =>
		PropertyExists( parent, name ) ? typeof(TValue) : null;

	ITrackProperty<T> ITrackPropertyFactory<TParent>.CreateProperty<T>( TParent parent, string name ) =>
		(ITrackProperty<T>)CreateProperty( parent, name );
}

public static class TrackProperty
{
	[SkipHotload]
	private static ImmutableArray<ITrackPropertyFactory>? _factories;

	private static IReadOnlyList<ITrackPropertyFactory> Factories => _factories ??=
		TypeLibrary.GetTypes<ITrackPropertyFactory>()
			.Where( x => x is { IsAbstract: false, IsInterface: false, IsGenericType: false } )
			.Select( CreateFactory )
			.OfType<ITrackPropertyFactory>()
			.OrderBy( x => x.Order )
			// ReSharper disable once UseCollectionExpression
			.ToImmutableArray();

	public static ITrackProperty? Create( ITrackTarget parent, string name )
	{
		var factory = Factories.FirstOrDefault( x => IsMatchingFactory( x, parent, name ) );
		var targetType = factory?.GetTargetType( parent, name );

		if ( factory is null || targetType is null ) return null;

		return GenericHelper.Get( targetType )
			.CreateProperty( factory, parent, name );
	}

	public static ITrackProperty Create( ITrackTarget parent, string name, Type targetType )
	{
		var factory = Factories.FirstOrDefault( x => IsMatchingFactory( x, parent, name, targetType ) )
			?? throw new Exception( $"We should have at least found the UnknownPropertyFactory." );

		return GenericHelper.Get( targetType )
			.CreateProperty( factory, parent, name );
	}

	public static ITrackProperty<T> Create<T>( ITrackTarget parent, string name ) =>
		(ITrackProperty<T>)Create( parent, name, typeof( T ) );

	private static bool IsMatchingFactory( ITrackPropertyFactory factory,
		ITrackTarget parent, string name )
	{
		if ( factory.GetTargetType( parent, name ) is not { } valueType ) return false;
		return valueType != typeof(Unknown);
	}

	private static bool IsMatchingFactory( ITrackPropertyFactory factory,
		ITrackTarget parent, string name, Type targetType )
	{
		if ( factory.GetTargetType( parent, name ) is not { } valueType ) return false;
		return valueType == targetType || valueType == typeof(Unknown);
	}

	private static ITrackPropertyFactory? CreateFactory( TypeDescription factoryType )
	{
		try
		{
			return factoryType.Create<ITrackPropertyFactory>();
		}
		catch
		{
			return null;
		}
	}
}

/// <summary>
/// To get around TypeLibrary not having a way to call generic methods.
/// </summary>
file abstract class GenericHelper
{
	[SkipHotload]
	private static Dictionary<Type, GenericHelper> Cache { get; } = new();

	public static GenericHelper Get( Type propertyType )
	{
		if ( Cache.TryGetValue( propertyType, out var cached ) )
		{
			return cached;
		}

		cached = TypeLibrary.GetType( typeof(GenericHelper<>) ).CreateGeneric<GenericHelper>( [propertyType] );

		Cache[propertyType] = cached;

		return cached;
	}

	public abstract ITrackProperty CreateProperty( ITrackPropertyFactory factory, ITrackTarget parent, string name );
}

file sealed class GenericHelper<T> : GenericHelper
{
	public override ITrackProperty CreateProperty( ITrackPropertyFactory factory, ITrackTarget parent, string name )
	{
		return factory.CreateProperty<T>( parent, name );
	}
}
