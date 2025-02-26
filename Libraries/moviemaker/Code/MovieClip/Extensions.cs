using System.Diagnostics.CodeAnalysis;

namespace Sandbox.MovieMaker;

#nullable enable

public static class MovieClipExtensions
{
	/// <summary>
	/// How deeply are we nested? Root tracks have depth <c>0</c>.
	/// </summary>
	public static int GetDepth( this ITrack track ) => track.Parent is null ? 0 : track.Parent.GetDepth() + 1;

	public static bool TryGetValue<T>( this ITrack track, MovieTime time, [MaybeNullWhen( false )] out T value )
	{
		if ( track.GetBlock( time ) is { } block )
		{
			return block.TryGetValue( time, out value );
		}

		value = default;
		return false;
	}

	public static bool TryGetValue<T>( this IBlock block, MovieTime time, [MaybeNullWhen( false )] out T value )
	{
		if ( block is IValueBlock<T> valueBlock )
		{
			value = valueBlock.GetValue( time );
			return true;
		}

		value = default;
		return false;
	}
}
