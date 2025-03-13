using Sandbox.MovieMaker;
using System.Text.Json.Serialization;

namespace Editor.MovieMaker;

#nullable enable

[JsonDerivedType( typeof( PropertyBlockCrossFade<>), "CrossFade" )]
partial record PropertyBlock<T>
{
	public PropertyBlock<T> CrossFade( PropertyBlock<T> next, InterpolationMode mode, bool invert )
	{
		if ( next.TimeRange.End < TimeRange.Start )
		{
			throw new ArgumentException( "Can't cross fade to a block that ends before this block starts.", nameof( next ) );
		}

		var timeRange = TimeRange with { End = next.TimeRange.End };

		var from = Slice( timeRange );
		var to = next.Slice( timeRange );

		return new PropertyBlockCrossFade<T>( from, to, mode, invert ).Reduce();
	}

	IProjectPropertyBlock IProjectPropertyBlock.CrossFade( IProjectPropertyBlock next, InterpolationMode mode, bool invert ) =>
		CrossFade( (PropertyBlock<T>)next, mode, invert );
}

file sealed record PropertyBlockCrossFade<T>( PropertyBlock<T> From, PropertyBlock<T> To, InterpolationMode Mode, bool Invert )
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

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		return From.GetPaintHintTimes( timeRange )
			.Merge( To.GetPaintHintTimes( timeRange ) )
			.Merge( IPropertyBlock.GetSampleTimes( TimeRange, TimeRange.Start, int.MaxValue, 30 ) );
	}

	protected override PropertyBlock<T> OnReduce()
	{
		if ( From == To )
		{
			return From;
		}

		if ( _interpolator is null || Mode is InterpolationMode.None )
		{
			return From.Join( To.Slice( TimeRange.End ), false );
		}

		return this;
	}

	public override string ToString()
	{
		return $"CrossFade({(Invert ? "Out" : "In")} {{ From = {From}, To = {To}, Mode = {Mode} }}";
	}

	private static bool Validate( PropertyBlock<T> from, PropertyBlock<T> to )
	{
		if ( from.TimeRange != to.TimeRange )
		{
			throw new ArgumentException( "From and To blocks must exactly overlap.", nameof( To ) );
		}

		return true;
	}
}
