using Sandbox.MovieMaker;
using System.Reflection;
using Sandbox.Diagnostics;

namespace Editor.MovieMaker;

#nullable enable

public static class KeyframeExtensions
{
	public const float DefaultSampleRate = 30f;

	public static bool CanHaveKeyframes( this IMovieProperty property ) => property is IMemberMovieProperty;

	public static KeyframeCurve? ReadKeyframes( this MovieTrack track ) => track.ReadEditorData()?.Keyframes;

	public static void WriteKeyframes( this MovieTrack track, KeyframeCurve keyframes, float sampleRate = DefaultSampleRate )
	{
		Assert.AreEqual( track.PropertyType, keyframes.ValueType );

		typeof( KeyframeExtensions ).GetMethod( nameof( WriteKeyframesInternal ), BindingFlags.NonPublic | BindingFlags.Static )!
			.MakeGenericMethod( track.PropertyType )
			.Invoke( null, [track, keyframes, sampleRate] );
	}

	private static void WriteKeyframesInternal<T>( this MovieTrack track, KeyframeCurve<T> keyframes, float sampleRate )
	{
		// Write keyframes in editor data as a JsonObject, so in the future we can edit tracks in other ways

		if ( track.ReadEditorData() is MovieTrackEditorData<T> editorData )
		{
			track.WriteEditorData( editorData with { Keyframes = keyframes } );
		}
		else
		{
			track.WriteEditorData( new MovieTrackEditorData<T>( keyframes ) );
		}

		// Compile keyframe data into a fast format for playback


		if ( keyframes.CanInterpolate )
		{
			// Interpolated keyframes: sample at uniform time steps

			var startTime = keyframes.StartTime;
			var duration = keyframes.Duration;
			var sampleCount = Math.Max( 1, (int)MathF.Ceiling( sampleRate * duration ) );
			var samples = new T[sampleCount];

			for ( var i = 0; i < sampleCount; ++i )
			{
				var t = startTime + duration * i / sampleCount;
				samples[i] = keyframes.GetValue( t );
			}

			var data = new SamplesData<T>( sampleRate, SampleInterpolationMode.Linear, samples );

			if ( track.Blocks.Count != 1 )
			{
				track.RemoveBlocks();
				track.AddBlock( startTime, duration, data );
			}
			else
			{
				track.Blocks[0].StartTime = startTime;
				track.Blocks[0].Duration = duration;
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

				track.AddBlock( prev.Time, next is null ? null : next.Value.Time - prev.Time, new ConstantData<T>( prev.Value ) );
			}
		}
	}
}
