using System;
using System.Collections.Immutable;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <summary>
/// Helper methods for working with <see cref="Clip"/>, <see cref="Track"/>, or <see cref="Block"/>.
/// </summary>
public static class CompiledClipExtensions
{
	/// <summary>
	/// Create a nested <see cref="ReferenceTrack"/> that targets a <see cref="Sandbox.GameObject"/> with
	/// the given <paramref name="name"/>.
	/// </summary>
	public static ReferenceTrack<GameObject> GameObject( this ReferenceTrack<GameObject> track, string name ) =>
		new( Guid.NewGuid(), name, track );

	/// <summary>
	/// Create a nested <see cref="ReferenceTrack"/> that targets a <see cref="Sandbox.Component"/> with
	/// the given <paramref name="type"/>.
	/// </summary>
	public static ReferenceTrack Component( this ReferenceTrack<GameObject> track, Type type ) =>
		TypeLibrary.GetType( typeof(ReferenceTrack<>) )
			.CreateGeneric<ReferenceTrack>( [type], [Guid.NewGuid(), type.Name, track] );

	/// <summary>
	/// Create a nested <see cref="ReferenceTrack"/> that targets a <see cref="Sandbox.Component"/> with
	/// the type <typeparamref name="T"/>.
	/// </summary>
	public static ReferenceTrack<T> Component<T>( this ReferenceTrack<GameObject> track ) =>
		new( Guid.NewGuid(), typeof(T).Name, track );

	/// <summary>
	/// Create a nested <see cref="PropertyTrack"/> that targets a property with the given <paramref name="name"/>
	/// in the parent track.
	/// </summary>
	public static PropertyTrack<T> Property<T>( this Track track, string name ) => new( name, track, ImmutableArray<PropertyBlock<T>>.Empty );

	/// <summary>
	/// Returns a clone of <paramref name="track"/> with an appended <see cref="ConstantBlock{T}"/> with the given
	/// <paramref name="timeRange"/> and <paramref name="value"/>.
	/// </summary>
	public static PropertyTrack<T> WithConstant<T>( this PropertyTrack<T> track,
		MovieTimeRange timeRange, T value )
	{
		return track with { Blocks = [..track.Blocks, new ConstantBlock<T>( timeRange, value )] };
	}

	/// <summary>
	/// Returns a clone of <paramref name="track"/> with an appended <see cref="SampleBlock{T}"/> with the given
	/// <paramref name="timeRange"/>, <paramref name="sampleRate"/>, and list of sample <paramref name="values"/>.
	/// </summary>
	public static PropertyTrack<T> WithSamples<T>( this PropertyTrack<T> track,
		MovieTimeRange timeRange, int sampleRate, IEnumerable<T> values )
	{
		return track with
		{
			Blocks = [..track.Blocks, new SampleBlock<T>( timeRange, 0d, sampleRate, [..values] )]
		};
	}
}
