using Sandbox.MovieMaker;
using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

partial class TrackView
{
	private readonly List<ITrackBlock> _blocks = new();

	private bool _blocksInvalid = true;

	public IEnumerable<ITrackBlock> Blocks
	{
		get
		{
			UpdateBlocks();
			return _blocks;
		}
	}

	private void UpdateBlocks()
	{
		if ( !_blocksInvalid ) return;

		_blocksInvalid = false;
		_blocks.Clear();

		foreach ( var child in Children )
		{
			AddChildBlocks( _blocks, child.Blocks );
		}

		if ( Track is IProjectPropertyTrack propertyTrack )
		{
			_blocks.AddRange( propertyTrack.Blocks.Except( _changedBlocks ) );
		}

		if ( Track is ProjectSequenceTrack sequenceTrack )
		{
			_blocks.AddRange( sequenceTrack.Blocks );
		}
	}

	/// <summary>
	/// Merge the current <paramref name="list"/> with blocks from <paramref name="blocks"/>, assuming
	/// both are already sorted.
	/// </summary>
	private void AddChildBlocks( List<ITrackBlock> list, IEnumerable<ITrackBlock> blocks )
	{
		if ( list.Count == 0 )
		{
			foreach ( var block in blocks )
			{
				list.Add( new PropertyBlock<object?>( DefaultSignal, block.TimeRange ) );
			}

			return;
		}

		var union = list
			.Select( x => x.TimeRange )
			.Union( blocks.Select( x => x.TimeRange ) )
			.ToArray();

		list.Clear();
		list.AddRange( union.Select( x => new PropertyBlock<object?>( DefaultSignal, x ) ) );
	}
}
