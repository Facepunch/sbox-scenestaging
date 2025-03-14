using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial record PropertyBlock<T>
{
	public PropertyBlock<T> Slice( MovieTimeRange timeRange ) => timeRange == TimeRange ? this : OnSlice( timeRange ).Reduce();
	protected abstract PropertyBlock<T> OnSlice( MovieTimeRange timeRange );

	public PropertyBlock<T> Shift( MovieTime offset ) => offset == default ? this : OnShift( offset ).Reduce();
	protected abstract PropertyBlock<T> OnShift( MovieTime offset );

	IProjectPropertyBlock IProjectPropertyBlock.Slice( MovieTimeRange timeRange ) => Slice( timeRange );
	IProjectPropertyBlock IProjectPropertyBlock.Shift( MovieTime offset ) => Shift( offset );
}
