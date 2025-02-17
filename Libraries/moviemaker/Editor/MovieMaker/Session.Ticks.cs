namespace Editor.MovieMaker;

#nullable enable

public enum TickStyle
{
	TimeLabel,
	Major,
	Minor
}

public record struct TimelineTick( TickStyle Style, float Interval );

partial class Session
{
	private const float MinMajorTickWidth = 70f;
	private const float MinMinorTickWidth = 6f;

	public TimelineTick MajorTick
	{
		get
		{
			const float baseMajorTime = 1f;

			var majorTime = baseMajorTime;

			foreach ( var tickScale in TickScales )
			{
				if ( TimeToPixels( majorTime ) < MinMajorTickWidth )
				{
					majorTime = baseMajorTime * tickScale;
				}
				else
				{
					break;
				}
			}

			return new TimelineTick( TickStyle.Major, majorTime );
		}
	}

	public TimelineTick MinorTick
	{
		get
		{
			var minorTime = FrameSnap ? Math.Max( MajorTick.Interval, 1f ) / FrameRate : 0.1f;
			var minorWidth = TimeToPixels( minorTime );

			while ( minorWidth < MinMinorTickWidth )
			{
				minorTime *= 2f;
				minorWidth *= 2f;
			}

			return new TimelineTick( TickStyle.Minor, minorTime );
		}
	}

	public IEnumerable<TimelineTick> Ticks
	{
		get
		{
			var major = MajorTick;

			yield return major with { Style = TickStyle.TimeLabel };
			yield return major;
			yield return MinorTick;
		}
	}

	private static IEnumerable<int> TickScales
	{
		get
		{
			for ( var i = 1; i <= 1_000_000; i *= 10 )
			{
				yield return i;
				yield return i * 2;
				yield return i * 5;
			}
		}
	}
}

