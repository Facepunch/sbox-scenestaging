using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class TrackView
{
	private readonly HashSet<ITrackBlock> _changedBlocks = new();
	private readonly List<ITrackBlock> _previewBlocks = new();
	private readonly List<ITrackBlock> _flattenedPreviewBlocks = new();

	private bool _previewBlocksInvalid = true;

	public IEnumerable<ITrackBlock> PreviewBlocks
	{
		get
		{
			UpdatePreviewBlocks();
			return _flattenedPreviewBlocks;
		}
	}

	private void UpdatePreviewBlocks()
	{
		if ( !_previewBlocksInvalid ) return;

		_previewBlocksInvalid = false;
		_flattenedPreviewBlocks.Clear();

		foreach ( var child in Children )
		{
			AddChildBlocks( _flattenedPreviewBlocks, child.PreviewBlocks );
		}

		_flattenedPreviewBlocks.AddRange( _previewBlocks );
	}

	public void SetPreviewBlocks( IEnumerable<ITrackBlock> original, IEnumerable<ITrackBlock> changed )
	{
		_changedBlocks.Clear();

		foreach ( var block in original )
		{
			_changedBlocks.Add( block );
		}

		_previewBlocks.Clear();
		_previewBlocks.AddRange( changed );

		MarkValueChanged();
	}

	public void ClearPreviewBlocks()
	{
		if ( _previewBlocks.Count == 0 ) return;

		_previewBlocks.Clear();

		MarkValueChanged();
	}
}
