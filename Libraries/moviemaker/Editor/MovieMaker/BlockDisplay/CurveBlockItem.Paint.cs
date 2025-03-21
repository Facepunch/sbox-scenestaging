using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

partial class CurveBlockItem<T>
{
	private GraphicsLine[]? Lines { get; set; }

	private IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange )
	{
		return Block switch
		{
			IPaintHintBlock paintHintBlock => paintHintBlock.GetPaintHints( timeRange ),
			CompiledSampleBlock<T> => [timeRange.Clamp( Block.TimeRange )],
			_ => []
		};
	}

	protected virtual void GetCurveTimes( List<MovieTime> times )
	{
		var timeRange = Block.TimeRange;

		times.Add( timeRange.Start );

		void TryAddTime( MovieTime time )
		{
			if ( time < timeRange.Start ) return;
			if ( time > timeRange.End ) return;

			if ( times[^1] < time )
			{
				times.Add( time );
			}
		}

		foreach ( var hintRange in GetPaintHints( timeRange ) )
		{
			TryAddTime( hintRange.Start );

			var clamped = hintRange with { End = hintRange.End - MovieTime.Epsilon };

			foreach ( var time in clamped.GetSampleTimes( 30 ) )
			{
				TryAddTime( time );
			}

			TryAddTime( hintRange.End - MovieTime.Epsilon );
		}

		TryAddTime( timeRange.End );
	}

	private void UpdateRanges()
	{
		if ( Elements.Count < 1 ) return;

		var times = Static.UpdateRanges_Times ??= new();

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

			var value = Block.GetValue( t );
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

		foreach ( var preview in Parent.BlockItems )
		{
			if ( preview is not ICurveBlockItem { Ranges: { } curveRanges } ) continue;
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
			Lines[j].PrepareGeometryChange();
			Lines[j].Position = new Vector2( 0f, margin );
			Lines[j].Size = new Vector2( LocalRect.Width, height );
		}

		var scale = range <= 0f ? 0f : height / range;

		// Second pass, update lines

		var t0 = TimeRange.Start;
		var t1 = TimeRange.End;

		var dxdt = LocalRect.Width / (t1 - t0).TotalSeconds;

		foreach ( var t in times )
		{
			var value = Block.GetValue( t );
			var x = LocalRect.Left + (float)((t - t0).TotalSeconds * dxdt);

			Decompose( value, floats );

			for ( var j = 0; j < Elements.Count; ++j )
			{
				var y = (mids[j] - floats[j]) * scale + 0.5f * height;

				if ( t <= Block.TimeRange.Start )
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
	[field: ThreadStatic]
	public static List<MovieTime>? UpdateRanges_Times { get; set; }
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
