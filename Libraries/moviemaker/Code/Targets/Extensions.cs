namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Helper methods for working with <see cref="TrackTargetMap"/> or <see cref="ITarget"/>.
/// </summary>
public static class TargetExtensions
{
	/// <summary>
	/// Tries to find a target mapped to the given track that represents a <see cref="GameObject"/> reference, returning <see langword="null"/> if not found.
	/// </summary>
	public static IGameObjectReference? GetGameObject( this TrackTargetMap map, ITrack track ) =>
		map.GetOrCreate( track ) as IGameObjectReference;

	/// <summary>
	/// Tries to find a target mapped to the given track that represents a <see cref="Component"/> reference, returning <see langword="null"/> if not found.
	/// </summary>
	public static IComponentReference? GetComponent( this TrackTargetMap map, ITrack track ) =>
		map.GetOrCreate( track ) as IComponentReference;

	public static IProperty? GetProperty( this TrackTargetMap map, ITrack track ) =>
		map.GetOrCreate( track ) as IProperty;

	public static IProperty<T>? GetProperty<T>( this TrackTargetMap map, ITrack track ) =>
		map.GetOrCreate( track ) as IProperty<T>;
}
