using System;

namespace Sandbox.MovieMaker;

#nullable enable

partial class MovieProperties
{
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

	public void ApplyFrame( IMovieTrackDescription track, IMovieBlock block, MovieTime time )
	{
		if ( block.Data is not IValueData valueData ) return;
		if ( this[track] is not IMemberProperty { IsBound: true, CanWrite: true } property ) return;

		property.Value = valueData.GetValue( time - block.TimeRange.Start );
	}
}
