using Sandbox.MovieMaker;
using Sandbox.UI;
using System.Reflection;

namespace Editor.MovieMaker;

#nullable enable

partial interface IProjectPropertyBlock
{
	IProjectPropertyBlock Slice( MovieTimeRange timeRange );
	IProjectPropertyBlock Shift( MovieTime offset );
}

partial class PropertyBlock<T>
{
	public PropertyBlock<T> Slice( MovieTimeRange timeRange )
	{
		if ( timeRange == TimeRange ) return this;

		// Easy constant cases

		if ( timeRange.End <= TimeRange.Start )
		{
			return Constant( GetValue( TimeRange.Start ), timeRange );
		}

		if ( timeRange.Start >= TimeRange.End )
		{
			return Constant( GetValue( TimeRange.End ), timeRange );
		}

		if ( timeRange.IsEmpty )
		{
			return Constant( GetValue( timeRange.Start ), timeRange );
		}

		// Let custom block types implement slicing the interior

		var result = OnSlice( timeRange.Clamp( TimeRange ) ).Reduce();

		// We clamp to this block's range, so extend the start / end with constant blocks if needed

		if ( timeRange.Start < TimeRange.Start )
		{
			result = Constant( GetValue( TimeRange.Start ) ) + result;
		}

		if ( timeRange.End > TimeRange.End )
		{
			result += Constant( GetValue( TimeRange.End ) );
		}

		return result;
	}

	/// <summary>
	/// Creates a block that covers a subset of this block's <see cref="TimeRange"/>. The resulting block will have exactly the given
	/// <paramref name="timeRange"/>, and always agree with this block when calling <see cref="GetValue"/> with times inside that range.
	/// </summary>
	/// <param name="timeRange">Desired slice time range. Guaranteed to be contained within this block's <see cref="TimeRange"/>.</param>
	protected abstract PropertyBlock<T> OnSlice( MovieTimeRange timeRange );

	public PropertyBlock<T> Shift( MovieTime offset ) => offset == default ? this : OnShift( offset ).Reduce();
	protected abstract PropertyBlock<T> OnShift( MovieTime offset );

	IProjectPropertyBlock IProjectPropertyBlock.Slice( MovieTimeRange timeRange ) => Slice( timeRange );
	IProjectPropertyBlock IProjectPropertyBlock.Shift( MovieTime offset ) => Shift( offset );
}
