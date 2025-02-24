using Editor.MovieMaker;

namespace Editor.TrackPainter;

#nullable enable

partial class CurvePreview<T>
{
	private GraphicsLine[]? Lines { get; set; }

	protected virtual void GetCurveTimes( List<float> times )
	{
		if ( Constant is { } constant )
		{
			times.Add( 0f );
			times.Add( Duration );
		}
		else if ( Samples is { } samples )
		{
			var dt = 1f / samples.SampleRate;

			for ( var i = 0; i <= samples.Samples.Count; ++i )
			{
				times.Add( i * dt );
			}
		}
	}

	protected T GetValue( float time )
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

		Span<float> mins = stackalloc float[Elements.Count];
		Span<float> maxs = stackalloc float[Elements.Count];
		Span<float> mids = stackalloc float[Elements.Count];

		for ( var j = 0; j < Elements.Count; ++j )
		{
			mins[j] = Elements[j].Min ?? float.PositiveInfinity;
			maxs[j] = Elements[j].Max ?? float.NegativeInfinity;
		}

		// First pass, find mins and maxs

		foreach ( var t in times )
		{
			if ( t < 0f ) continue;

			var value = GetValue( t );
			Decompose( value, floats );

			for ( var j = 0; j < Elements.Count; ++j )
			{
				mins[j] = Math.Min( mins[j], floats[j] );
				maxs[j] = Math.Max( maxs[j], floats[j] );
			}
		}

		var range = 0f;

		for ( var j = 0; j < Elements.Count; ++j )
		{
			range = Math.Max( range, maxs[j] - mins[j] );

			mids[j] = (mins[j] + maxs[j]) * 0.5f;

			Lines[j].Clear();
			Lines[j].Position = new Vector2( 0f, margin );
			Lines[j].Size = new Vector2( LocalRect.Width, height );
		}

		var scale = range <= 0f ? 0f : height / range;

		// Second pass, update lines

		var t0 = 0f;
		var t1 = Duration;

		var dxdt = LocalRect.Width / (t1 - t0);

		foreach ( var t in times )
		{
			var value = GetValue( t );
			var x = LocalRect.Left + (t - t0) * dxdt;

			Decompose( value, floats );

			for ( var j = 0; j < Elements.Count; ++j )
			{
				var y = (mids[j] - floats[j]) * scale + 0.5f * height;

				if ( t <= 0f )
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
	public static List<float>? PaintCurve_Times { get; set; }
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
