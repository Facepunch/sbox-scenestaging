using System.Collections;
using System.Collections.Immutable;

namespace Sandbox.MovieMaker;

/// <summary>
/// An <c>IReadOnlyList&lt;object?&gt;</c> wrapping an <see cref="ImmutableArray{T}"/>.
/// </summary>
internal sealed record ReadOnlyListWrapper<TSrc, TDst>( ImmutableArray<TSrc> Array ) : IReadOnlyList<TDst>
	where TSrc : TDst
{
	public int Count => Array.Length;

	public TDst this[int index] => Array[index];

	public IEnumerator<TDst> GetEnumerator() => Array.Cast<TDst>().GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
