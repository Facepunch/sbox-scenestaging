using System;

namespace Sandbox.MovieMaker;

#nullable enable

partial class MovieProperties
{
	/// <summary>
	/// For each track in the given <paramref name="clip"/> that we have a mapped property for,
	/// set the property value to whatever value is stored in that track at the given <paramref name="time"/>.
	/// </summary>
	public void ApplyFrame( MovieClip clip, MovieTime time )
	{
		if ( time > clip.Duration ) return;
		if ( time < MovieTime.Zero ) return;

		using var sceneScope = scene.Push();

		foreach ( var track in clip.RootTracks )
		{
			ApplyFrame( track, time );
		}
	}

	/// <summary>
	/// For each descendant of <paramref name="track"/> (including itself) that we have a mapped property for,
	/// set the property value to whatever value is stored in that track at the given <paramref name="time"/>.
	/// </summary>
	private void ApplyFrame( MovieTrack track, MovieTime time )
	{
		if ( track.GetBlock( time ) is { } block )
		{
			ApplyFrame( track, block, time );
		}

		foreach ( var child in track.Children )
		{
			ApplyFrame( child, time );
		}
	}

	/// <summary>
	/// If we have a mapped property for <paramref name="track"/>, set the property value to whatever value
	/// is stored in <paramref name="block"/> at the given <paramref name="time"/>.
	/// </summary>
	public void ApplyFrame( IMovieTrackDescription track, IMovieBlock block, MovieTime time )
	{
		if ( block.Data is not IValueData valueData ) return;
		if ( GetMember( track ) is not { IsBound: true, CanWrite: true } property ) return;

		property.Value = valueData.GetValue( time - block.TimeRange.Start );
	}
}
