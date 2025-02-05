using System;
using Sandbox.Diagnostics;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A time region in a <see cref="MovieTrack"/> where something happens.
/// </summary>
public partial class MovieBlock
{
	private MovieTrack? _track;
	private MovieBlockData _data;

	public MovieTrack Track => _track ?? throw new Exception( $"{nameof(MovieBlock)} has been removed." );
	public MovieClip Clip => Track.Clip;

	public int Id { get; }

	/// <summary>
	/// Time that the block starts, in seconds.
	/// </summary>
	public float StartTime { get; set; }

	/// <summary>
	/// Duration of the block, in seconds. If null, it lasts until the end of the clip.
	/// </summary>
	public float? Duration { get; set; }

	/// <summary>
	/// Track data for this block. Either a constant, sample array, or invoked action information.
	/// </summary>
	public MovieBlockData Data
	{
		get => _data;
		set
		{
			AssertValidData( value );
			_data = value;
		}
	}

	internal MovieBlock( MovieTrack track, int id, float startTime, float? duration, MovieBlockData data )
	{
		_track = track;

		Id = id;

		StartTime = startTime;
		Duration = duration;

		AssertValidData( data );

		_data = data;
	}

	public void Remove()
	{
		_track?.RemoveBlockInternal( this );

		InvalidateInternal();
	}

	internal void InvalidateInternal()
	{
		_track = null;
	}

	public bool Contains( float time ) => time >= StartTime && (Duration is null || time - StartTime < Duration);

	private void AssertValidData( MovieBlockData value )
	{
		switch ( value )
		{
			case IConstantData constantData:
				Assert.True( constantData.ValueType.IsAssignableTo( Track.PropertyType ),
					"Incompatible constant value type." );
				break;

			case ISamplesData samplesData:
				Assert.True( samplesData.ValueType.IsAssignableTo( Track.PropertyType ),
					"Incompatible sample value type." );
				break;

			case ActionData:
				throw new NotImplementedException();

			case null:
				throw new ArgumentNullException( nameof(value) );
		}
	}
}

/// <summary>
/// Base type for data describing how a track changes during a <see cref="MovieBlock"/>.
/// </summary>
public abstract record MovieBlockData;
