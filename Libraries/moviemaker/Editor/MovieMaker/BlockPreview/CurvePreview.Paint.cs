using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockPreviews;

#nullable enable

partial class CurvePreview<T>
{
	private GraphicsLine[]? Lines { get; set; }

	protected virtual void GetCurveTimes( List<MovieTime> times )
	{
		if ( Constant is not null )
		{
			times.Add( MovieTime.Zero );
			times.Add( TimeRange.Duration );
		}
		else if ( Samples is { } samples )
		{
			for ( var i = 0; i < samples.Samples.Count; ++i )
			{
				var time = MovieTime.FromFrames( i, samples.SampleRate );

				if ( time >= TimeRange.Duration ) break;

				times.Add( time );
			}

			times.Add( TimeRange.Duration );
		}
	}

	protected T GetValue( MovieTime time )
	{
		if ( Samples is { } samples )
		{
			return samples.GetValue( time );
		}

		if ( Constant is { } constant )
		{
			return constant.Value;
		}

		return default!;
	}

	private void UpdateRanges()
	{
		if ( Elements.Count < 1 ) return;

		var times = Static.PaintCurve_Times ??= new();

		times.Clear();

		GetCurveTimes( times );

		if ( times.Count == 0 ) return;

		Span<float> floats = stackalloc float[Elements.Count];

		for ( var j = 0; j < Elements.Count; ++j )
		{
			_ranges[j] = (Elements[j].Min ?? float.PositiveInfinity, Elements[j].Max ?? float.NegativeInfinity);
		}

		foreach ( var t in times )
		{
			if ( t.IsNegative ) continue;

			var value = GetValue( t );
			Decompose( value, floats );

			for ( var j = 0; j < Elements.Count; ++j )
			{
				_ranges[j] = (Math.Min( _ranges[j].Min, floats[j] ), Math.Max( _ranges[j].Max, floats[j] ));
			}
		}
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		if ( Elements.Count < 1 ) return;

		if ( Lines is null )
		{
			Lines = new GraphicsLine[Elements.Count];

			for ( var i = 0; i < Elements.Count; ++i )
			{
				Lines[i] = new CurveLine( this, Elements[i].Color );
			}
		}

		foreach ( var line in Lines )
		{
			line.Clear();
		}

		var times = Static.PaintCurve_Times ??= new();

		times.Clear();

		GetCurveTimes( times );

		if ( times.Count == 0 ) return;

		var margin = 2f;
		var height = LocalRect.Height - margin * 2f;

		Span<float> floats = stackalloc float[Elements.Count];
		Span<(float Min, float Max)> ranges = stackalloc (float, float)[Elements.Count];
		Span<float> mids = stackalloc float[Elements.Count];

		_ranges.CopyTo( ranges );

		var range = 0f;

		// All previews on the same track should have the same range

		foreach ( var preview in Parent.BlockPreviews )
		{
			if ( preview is not ICurvePreview { Ranges: { } curveRanges } ) continue;
			if ( curveRanges.Count != ranges.Length ) continue;

			for ( var j = 0; j < Elements.Count; ++j )
			{
				ranges[j] = (Math.Min( ranges[j].Min, curveRanges[j].Min ), Math.Max( ranges[j].Max, curveRanges[j].Max ));
			}
		}

		for ( var j = 0; j < Elements.Count; ++j )
		{
			range = Math.Max( range, ranges[j].Max - ranges[j].Min );

			mids[j] = (ranges[j].Min + ranges[j].Max) * 0.5f;
		}

		for ( var j = 0; j < Elements.Count; ++j )
		{
			Lines[j].Clear();
			Lines[j].Position = new Vector2( 0f, margin );
			Lines[j].Size = new Vector2( LocalRect.Width, height );
		}

		var scale = range <= 0f ? 0f : height / range;

		// Second pass, update lines

		var t0 = MovieTime.Zero;
		var t1 = TimeRange.Duration;

		var dxdt = LocalRect.Width / (t1 - t0).TotalSeconds;

		foreach ( var t in times )
		{
			var value = GetValue( t );
			var x = LocalRect.Left + (float)((t - t0).TotalSeconds * dxdt);

			Decompose( value, floats );

			for ( var j = 0; j < Elements.Count; ++j )
			{
				var y = (mids[j] - floats[j]) * scale + 0.5f * height;

				if ( t <= MovieTime.Zero )
				{
					Lines[j].MoveTo( new Vector2( x, y ) );
				}
				else
				{
					Lines[j].LineTo( new Vector2( x, y ) );
				}
			}
		}
	}
}

file class Static
{
	[field: ThreadStatic]
	public static List<MovieTime>? PaintCurve_Times { get; set; }
}

file class CurveLine : GraphicsLine
{
	public Color Color { get; }

	public CurveLine( GraphicsItem parent, Color color )
		: base( parent )
	{
		Color = color;
	}

	protected override void OnPaint()
	{
		Paint.SetPen( Color.WithAlpha( 0.25f ), 2f );
		PaintLine();
	}
}
