using System;
using Sandbox.Diagnostics;

namespace Sandbox.MovieMaker;

#nullable enable

public interface IMovieBlock
{
	MovieTimeRange TimeRange { get; }
	IMovieBlockData Data { get; }
}

public record MovieBlockSlice( MovieTimeRange TimeRange, IMovieBlockData Data ) : IMovieBlock;

/// <summary>
/// A time region in a <see cref="MovieTrack"/> where something happens.
/// </summary>
public sealed partial class MovieBlock : IMovieBlock
{
	private MovieTrack? _track;
	private IMovieBlockData _data;
	private MovieTimeRange _timeRange;

	public MovieTrack Track => _track ?? throw new Exception( $"{nameof(MovieBlock)} has been removed." );
	public MovieClip Clip => Track.Clip;

	public int Id { get; }

	public MovieTimeRange TimeRange
	{
		get => _timeRange;
		set
		{
			_timeRange = value;
			_track?.BlockChangedInternal( this );
		}
	}

	public MovieTime TimeOffset { get; set; }
	public MovieTime Start => TimeRange.Start;
	public MovieTime End => TimeRange.End;
	public MovieTime Duration => TimeRange.Duration;

	/// <summary>
	/// Track data for this block. Either a constant, sample array, or invoked action information.
	/// </summary>
	public IMovieBlockData Data
	{
		get => _data;
		set
		{
			AssertValidData( value );
			_data = value;
		}
	}

	internal MovieBlock( MovieTrack track, int id, MovieTimeRange timeRange, IMovieBlockData data )
	{
		Id = id;

		_track = track;
		_timeRange = timeRange;

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

	private void AssertValidData( IMovieBlockData value )
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
public interface IMovieBlockData;

public interface IMovieBlockValueData : IMovieBlockData
{
	/// <summary>
	/// Property value type, must match <see cref="MovieTrack.PropertyType"/>.
	/// </summary>
	Type ValueType { get; }

	/// <summary>
	/// Samples the signal at the given <paramref name="time"/>, where <c>0</c> will return the first sample.
	/// </summary>
	object? GetValue( MovieTime time );

	void Sample( Array dstSamples, int dstOffset, int sampleCount, MovieTimeRange srcTimeRange, int sampleRate );
	IMovieBlockValueData Slice( MovieTimeRange timeRange );
}

public interface IMovieBlockValueData<T> : IMovieBlockValueData
{
	new T GetValue( MovieTime time );
	void Sample( Span<T> dstSamples, MovieTimeRange srcTimeRange, int sampleRate );
	new IMovieBlockValueData<T> Slice( MovieTimeRange timeRange );
}
