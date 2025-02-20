using Sandbox.MovieMaker;
using System.Reflection;
using Sandbox.Diagnostics;

namespace Editor.MovieMaker;

#nullable enable

public static class KeyframeExtensions
{
	public static bool CanHaveKeyframes( this IMovieProperty property ) => property is IMemberMovieProperty { CanWrite: true };

	public static KeyframeCurve? ReadKeyframes( this MovieTrack track ) =>
		(KeyframeCurve?)Json.FromNode( track.ReadEditorData()?.Keyframes, typeof(KeyframeCurve<>).MakeGenericType( track.PropertyType ) );

	public static void WriteKeyframes( this MovieTrack track, KeyframeCurve keyframes, int sampleRate )
	{
		Assert.AreEqual( track.PropertyType, keyframes.ValueType );

		typeof( KeyframeExtensions ).GetMethod( nameof( WriteKeyframesInternal ), BindingFlags.NonPublic | BindingFlags.Static )!
			.MakeGenericMethod( track.PropertyType )
			.Invoke( null, [track, keyframes, sampleRate] );
	}

	private static void WriteKeyframesInternal<T>( this MovieTrack track, KeyframeCurve<T> keyframes, int sampleRate )
	{
		// Write keyframes in editor data as a JsonObject, so in the future we can edit tracks in other ways

		var node = Json.ToNode( keyframes, typeof(KeyframeCurve<T>) );

		if ( track.ReadEditorData() is { } editorData )
		{
			track.WriteEditorData( editorData with { Keyframes = node } );
		}
		else
		{
			track.WriteEditorData( new MovieTrackEditorData( Keyframes: node ) );
		}

		// Compile keyframe data into a fast format for playback

		if ( keyframes.CanInterpolate )
		{
			// Interpolated keyframes: sample at uniform time steps

			var timeRange = keyframes.TimeRange;
			var sampleCount = Math.Max( 1, timeRange.Duration.GetFrameCount( sampleRate ) );
			var samples = new T[sampleCount];

			for ( var i = 0; i < sampleCount; ++i )
			{
				samples[i] = keyframes.GetValue( MovieTime.FromFrames( i, sampleRate ) );
			}

			var data = new SamplesData<T>( sampleRate, SampleInterpolationMode.Linear, samples );

			if ( track.Blocks.Count != 1 )
			{
				track.RemoveBlocks();
				track.AddBlock( keyframes.TimeRange, data );
			}
			else
			{
				track.Blocks[0].TimeRange = keyframes.TimeRange;
				track.Blocks[0].Data = data;
			}
		}
		else
		{
			// Not interpolated: constant blocks

			// TODO: keep blocks around, just change values?

			track.RemoveBlocks();

			for ( var i = 0; i < keyframes.Count; ++i )
			{
				var prev = keyframes[i];
				var next = i < keyframes.Count - 1 ? (Keyframe<T>?) keyframes[i + 1] : null;

				track.AddBlock( new MovieTimeRange( prev.Time, (next ?? prev).Time ),
					new ConstantData<T>( prev.Value ) );
			}
		}
	}
}
