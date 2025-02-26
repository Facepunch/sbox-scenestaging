using System;

namespace Sandbox.MovieMaker;

#nullable enable

partial class MovieTargets
{
	/// <summary>
	/// For each track in the given <paramref name="clip"/> that we have a mapped property for,
	/// set the property value to whatever value is stored in that track at the given <paramref name="time"/>.
	/// </summary>
	public void ApplyFrame( IClip clip, MovieTime time )
	{
		if ( time > clip.Duration ) return;
		if ( time < MovieTime.Zero ) return;

		using var sceneScope = scene.Push();

		foreach ( var track in clip.Tracks )
		{
			ApplyFrame( track, time );
		}
	}

	/// <summary>
	/// If we have a mapped property for <paramref name="track"/>, set the property value to whatever value
	/// is stored in that track at the given <paramref name="time"/>.
	/// </summary>
	public void ApplyFrame( ITrack track, MovieTime time )
	{
		if ( track.GetBlock( time ) is { } block )
		{
			ApplyFrame( track, block, time );
		}
	}

	/// <summary>
	/// If we have a mapped property for <paramref name="track"/>, set the property value to whatever value
	/// is stored in <paramref name="block"/> at the given <paramref name="time"/>.
	/// </summary>
	public void ApplyFrame( ITrack track, IBlock block, MovieTime time )
	{
		if ( block is not IValueBlock valueBlock ) return;
		if ( GetMember( track ) is not { IsBound: true, CanWrite: true } property ) return;

		property.Value = valueBlock.GetValue( time - block.TimeRange.Start );
	}
}
