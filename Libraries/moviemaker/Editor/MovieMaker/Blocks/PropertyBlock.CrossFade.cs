using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial record PropertyBlock<T>
{
	public PropertyBlock<T> CrossFade( PropertyBlock<T> next, InterpolationMode mode = InterpolationMode.Linear, bool invert = false )
	{
		if ( next.TimeRange.End < TimeRange.Start )
		{
			throw new ArgumentException( "Can't cross fade to a block that ends before this block starts.", nameof( next ) );
		}

		var timeRange = TimeRange with { End = next.TimeRange.End };

		if ( timeRange.IsEmpty )
		{
			return Slice( timeRange );
		}

		var from = Slice( timeRange );
		var to = next.Slice( timeRange );

		var fromParts = from.TrySplit();
		var toParts = to.TrySplit();

		if ( fromParts == null && toParts == null )
		{
			return new PropertyBlockCrossFade<T>( from, to, mode, invert );
		}

		fromParts ??= [from];
		toParts ??= [to];

		var resultParts = new List<PropertyBlock<T>>();

		foreach ( var fromPart in fromParts )
		{
			foreach ( var toPart in toParts )
			{
				if ( fromPart.TimeRange.Intersect( toPart.TimeRange ) is not { IsEmpty: false } intersection ) continue;

				Log.Info( $"{fromPart.TimeRange}, {toPart.TimeRange}, {intersection}" );
				resultParts.Add( fromPart.Slice( intersection ).CrossFade( toPart.Slice( intersection ), timeRange, mode, invert ) );
			}
		}

		return resultParts.Join();
	}

	public PropertyBlock<T> CrossFade( PropertyBlock<T> next,
		MovieTimeRange fadeRange, InterpolationMode mode = InterpolationMode.Linear, bool invert = false )
	{
		if ( next.TimeRange.End < TimeRange.Start )
		{
			throw new ArgumentException( "Can't cross fade to a block that ends before this block starts.", nameof( next ) );
		}

		var timeRange = TimeRange with { End = next.TimeRange.End };

		// No fade if fadeRange starts after timeRange

		if ( fadeRange.Start > timeRange.End )
		{
			return this;
		}

		// No fade if fadeRange ends before timeRange

		if ( fadeRange.End < timeRange.Start )
		{
			return next;
		}

		var before = this.ClampEnd( fadeRange.Start );
		var after = next.ClampStart( fadeRange.End );

		// Hard join if fadeRange duration is zero

		if ( fadeRange.IsEmpty )
		{
			return before.Join( after );
		}

		var fade = Slice( fadeRange )
			.CrossFade( next.Slice( fadeRange ), mode, invert )
			.Clamp( timeRange );

		return before.Join( fade ).Join( after );
	}

	IProjectPropertyBlock IProjectPropertyBlock.CrossFade( IProjectPropertyBlock next, InterpolationMode mode, bool invert ) =>
		CrossFade( (PropertyBlock<T>)next, mode, invert );
}

[JsonDiscriminator( "CrossFade" )]
file sealed record PropertyBlockCrossFade<T>( PropertyBlock<T> From, PropertyBlock<T> To,
	[property: JsonPropertyOrder( -2 )] InterpolationMode Mode,
	[property: JsonPropertyOrder( -1 ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )] bool Invert )
	: PropertyBlock<T>( (From.TimeRange.Start, To.TimeRange.End) )
{
	private readonly bool _validated = Validate( From, To );

	private readonly IInterpolator<T>? _interpolator = Interpolator.GetDefault<T>();

	public override T GetValue( MovieTime time )
	{
		var fraction = TimeRange.GetFraction( time );
		var blend = Invert
			? 1f - Mode.Apply( 1f - fraction )
			: Mode.Apply( fraction );

		if ( blend <= 0f || _interpolator is not { } interpolator )
		{
			return From.GetValue( time );
		}

		if ( blend >= 1f )
		{
			return To.GetValue( time );
		}

		return interpolator.Interpolate( From.GetValue( time ), To.GetValue( time ), blend );
	}

	protected override PropertyBlock<T> OnShift( MovieTime offset )
	{
		return this with { From = From.Shift( offset ), To = To.Shift( offset ) };
	}

	protected override PropertyBlock<T> OnSlice( MovieTimeRange timeRange )
	{
		return this with { From = From.Slice( timeRange ), To = To.Slice( timeRange ) };
	}

	protected override PropertyBlock<T> OnReduce()
	{
		if ( From == To )
		{
			return From;
		}

		if ( _interpolator is null || Mode is InterpolationMode.None )
		{
			return From.Join( To.Slice( TimeRange.End ) );
		}

		return this;
	}

	private static bool Validate( PropertyBlock<T> from, PropertyBlock<T> to )
	{
		if ( from.TimeRange != to.TimeRange )
		{
			throw new ArgumentException( "From and To blocks must exactly overlap.", nameof( To ) );
		}

		return true;
	}

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		return From.GetPaintHintTimes( timeRange )
			.Merge( To.GetPaintHintTimes( timeRange ) )
			.Merge( IPropertyBlock.GetSampleTimes( TimeRange, TimeRange.Start, int.MaxValue, 30 ) );
	}
}
