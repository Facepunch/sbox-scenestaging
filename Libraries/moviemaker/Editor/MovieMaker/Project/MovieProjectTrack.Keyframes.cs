using Sandbox.Diagnostics;
using Sandbox.MovieMaker;
using System.Reflection;

namespace Editor.MovieMaker;

#nullable enable

partial class ProjectTrack
{
	public KeyframeCurve? Keyframes { get; set; }

	public void WriteKeyframes( KeyframeCurve keyframes, int sampleRate )
	{
		Assert.AreEqual( TargetType, keyframes.ValueType );

		typeof( ProjectTrack ).GetMethod( nameof( WriteKeyframesInternal ), BindingFlags.NonPublic | BindingFlags.Static )!
			.MakeGenericMethod( TargetType )
			.Invoke( this, [keyframes, sampleRate] );
	}

	private void WriteKeyframesInternal<T>( KeyframeCurve<T> keyframes, int sampleRate )
	{
		// Compile keyframe data into a fast format for playback

		if ( keyframes.CanInterpolate )
		{
			// Interpolated keyframes: sample at uniform time steps

			var timeRange = keyframes.TimeRange;
			var sampleCount = timeRange.Duration.GetFrameCount( sampleRate );
			var samples = new T[sampleCount];

			for ( var i = 0; i < sampleCount; ++i )
			{
				samples[i] = keyframes.GetValue( MovieTime.FromFrames( i, sampleRate ) );
			}

			var data = new SamplesData<T>( sampleRate, samples );

			if ( Blocks.Count != 1 )
			{
				RemoveBlocks();
				AddBlock( keyframes.TimeRange, data );
			}
			else
			{
				Blocks[0].TimeRange = keyframes.TimeRange;
				Blocks[0].Data = data;
			}
		}
		else
		{
			// Not interpolated: constant blocks

			// TODO: keep blocks around, just change values?

			RemoveBlocks();

			for ( var i = 0; i < keyframes.Count; ++i )
			{
				var prev = keyframes[i];
				var next = i < keyframes.Count - 1 ? (Keyframe<T>?)keyframes[i + 1] : null;

				AddBlock( new MovieTimeRange( prev.Time, (next ?? prev).Time ),
					new ConstantData<T>( prev.Value ) );
			}
		}
	}
}
