using System;
using System.Collections.Immutable;

namespace Sandbox.MovieMaker.Controllers;

#nullable enable

/// <summary>
/// Provides special handling for components while being directed by a <see cref="MoviePlayer"/>.
/// Useful for disabling physics etc. while being animated my a movie.
/// </summary>
internal interface IComponentDirector
{
	Component Component { get; init; }

	/// <summary>
	/// Which other components should be directed if <see cref="Component"/> is directed.
	/// </summary>
	IEnumerable<Component> AutoDirectedComponents => Enumerable.Empty<Component>();

	/// <summary>
	/// Called when a <see cref="MoviePlayer"/> starts directing <see cref="Component"/>.
	/// </summary>
	void Start( MoviePlayer player ) { }

	/// <summary>
	/// Called when a <see cref="MoviePlayer"/> is playing one animation frame while directing <see cref="Component"/>.
	/// </summary>
	void Update( MoviePlayer player,  MovieTime deltaTime ) { }

	/// <summary>
	/// Called when a <see cref="MoviePlayer"/> stops directing <see cref="Component"/>.
	/// </summary>
	void Stop( MoviePlayer player ) { }
}

/// <inheritdoc cref="IComponentDirector"/>
/// <typeparam name="T">Component type.</typeparam>
internal interface IComponentDirector<T> : IComponentDirector
	where T : Component
{
	public new T Component { get; init; }

	Component IComponentDirector.Component
	{
		get => Component;
		init => Component = (T)value;
	}
}

internal static class ComponentDirector
{
	[SkipHotload]
	private static ImmutableDictionary<Type, IComponentDirectorFactory>? _directorFactories;

	public static IComponentDirector? Create( Component component )
	{
		_directorFactories ??= CreateFactories();

		return _directorFactories.GetValueOrDefault( component.GetType() )?
			.Create( component );
	}

	private static ImmutableDictionary<Type, IComponentDirectorFactory> CreateFactories()
	{
		var dict = new Dictionary<Type, IComponentDirectorFactory>();
		var factoryType = TypeLibrary.GetType( typeof(ComponentDirectorFactory<,>) );

		foreach ( var type in TypeLibrary.GetTypes<IComponentDirector>() )
		{
			if ( type.IsAbstract ) continue;

			foreach ( var iface in type.Interfaces )
			{
				try
				{
					if ( TypeLibrary.GetType( iface )?.TargetType != typeof(IComponentDirector<>) ) continue;

					var componentType = TypeLibrary.GetGenericArguments( iface )[0];

					var factory = factoryType.CreateGeneric<IComponentDirectorFactory>(
						[componentType, type.TargetType] );

					dict.Add( componentType, factory );

					Log.Info( $"{componentType}: {type.FullName}" );
				}
				catch ( Exception ex )
				{
					Log.Warning( ex );
				}
			}
		}

		return dict.ToImmutableDictionary();
	}
}

internal interface IComponentDirectorFactory
{
	IComponentDirector Create( Component component );
}

file sealed class ComponentDirectorFactory<TComponent, TController> : IComponentDirectorFactory
	where TComponent : Component
	where TController : IComponentDirector<TComponent>, new()
{
	public IComponentDirector Create( Component component ) => new TController { Component = (TComponent)component };
}
