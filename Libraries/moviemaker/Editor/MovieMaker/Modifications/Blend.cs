using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public record BlendModificationOptions( bool IsAdditive, MovieTime Offset ) : ITrackModificationOptions;

public abstract class BlendModification<T> : ITrackModification<T, BlendModificationOptions>
{
	protected PropertyBlock<T> Blend( PropertySignal<T>? original, PropertySignal<T>? overlay,
		PropertySignal<T> relativeTo, MovieTimeRange timeRange, TimeSelection selection, BlendModificationOptions options )
	{
		if ( original is null && overlay is null )
		{
			throw new ArgumentNullException( nameof( overlay ), "Expected at least one signal." );
		}

		overlay += options.Offset;

		if ( original is null || overlay is null )
		{
			return new PropertyBlock<T>( (original ?? overlay)!.Reduce( timeRange ), timeRange );
		}

		if ( options.IsAdditive )
		{
			overlay = overlay - relativeTo + original;
		}

		return new PropertyBlock<T>( original.CrossFade( overlay, selection ).Reduce( timeRange ), timeRange );
	}

	public abstract IEnumerable<PropertyBlock<T>> Apply( IReadOnlyList<PropertyBlock<T>> original, TimeSelection selection, BlendModificationOptions options );
}

public interface ISignalBlendModification : ITrackModification
{
	ISignalBlendModification WithSignal( IPropertySignal signal );
}

public sealed class SignalBlendModification<T>( PropertySignal<T> signal, PropertySignal<T> relativeTo ) : BlendModification<T>, ISignalBlendModification
{
	public override IEnumerable<PropertyBlock<T>> Apply( IReadOnlyList<PropertyBlock<T>> original, TimeSelection selection, BlendModificationOptions options )
	{
		var timeRange = selection.TotalTimeRange;

		// Fill in gaps between blocks in original track with AsSignal()

		if ( original.AsSignal() is not { } originalSignal )
		{
			yield return new PropertyBlock<T>( signal, timeRange );
			yield break;
		}

		yield return Blend( originalSignal, signal, relativeTo, timeRange, selection, options );
	}

	public ISignalBlendModification WithSignal( IPropertySignal newSignal ) =>
		new SignalBlendModification<T>( (PropertySignal<T>)newSignal, relativeTo );
}

public sealed class ClipboardBlendModification<T>( ImmutableArray<PropertyBlock<T>> sourceBlocks ) : BlendModification<T>
{
	public ClipboardBlendModification( IEnumerable<IProjectPropertyBlock> blocks )
		: this( [..blocks.Cast<PropertyBlock<T>>()] )
	{

	}

	public override IEnumerable<PropertyBlock<T>> Apply( IReadOnlyList<PropertyBlock<T>> original, TimeSelection selection, BlendModificationOptions options )
	{
		var blocks = sourceBlocks;

		var timeRanges = original.Select( x => x.TimeRange )
			.Union( blocks.Select( x => x.TimeRange + options.Offset ) );

		PropertySignal<T> relativeTo = blocks[0].GetValue( blocks[0].TimeRange.Start );

		foreach ( var timeRange in timeRanges )
		{
			var originalSignal = original
				.Where( x => timeRange.Contains( x.TimeRange ) )
				.AsSignal();

			var overlaySignal = blocks
				.Where( x => timeRange.Contains( x.TimeRange + options.Offset ) )
				.AsSignal();

			yield return Blend( originalSignal, overlaySignal, relativeTo, timeRange, selection, options );
		}
	}
}

public static class TrackModificationExtensions
{
	public static ITrackModification AsModification( this IPropertySignal signal )
	{
		var modificationType = typeof( SignalBlendModification<> ).MakeGenericType( signal.PropertyType );

		return (ITrackModification)Activator.CreateInstance( modificationType, signal, signal )!;
	}

	public static ITrackModification AsModification( this IEnumerable<IProjectPropertyBlock> sourceBlocks )
	{
		var untypedArray = sourceBlocks.ToArray();

		if ( untypedArray.Length == 0 ) throw new ArgumentException( "Expected at least one block.", nameof( sourceBlocks ) );

		var propertyType = untypedArray[0].PropertyType;
		var modificationType = typeof( ClipboardBlendModification<> ).MakeGenericType( propertyType );

		return (ITrackModification)Activator.CreateInstance( modificationType, [untypedArray] )!;
	}
}
