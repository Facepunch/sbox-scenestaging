using Editor.MovieMaker.BlockDisplays;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public partial class DopeSheetTrack : GraphicsItem
{
	public TrackWidget TrackWidget { get; }

	private bool? _canCreatePreview;

	private readonly List<IMovieBlock> _blocks = new();
	private readonly List<BlockItem> _blockItems = new();

	public IReadOnlyList<BlockItem> BlockItems => _blockItems;

	public bool Visible => TrackWidget.Visible;

	public DopeSheetTrack( TrackWidget track )
	{
		TrackWidget = track;
		HoverEvents = true;
	}

	internal void DoLayout( Rect r )
	{
		PrepareGeometryChange();

		Position = new Vector2( 0, r.Top + 1 );
		Size = Visible ? new Vector2( 50000, r.Height ) : 0f;

		UpdateBlockItems();

		TrackWidget.TrackList.Session.EditMode?.TrackLayout( this, r );
	}

	private void ClearBlockItems()
	{
		if ( _blockItems.Count == 0 ) return;

		foreach ( var blockPreview in _blockItems )
		{
			blockPreview.Destroy();
		}

		_blockItems.Clear();
	}

	internal void OnSelected()
	{
		TrackWidget.InspectProperty();
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		base.OnMousePressed( e );

		if ( e.LeftMouseButton )
		{
			OnSelected();
		}
	}

	private void GetBlocks( List<IMovieBlock> result )
	{
		var track = TrackWidget.MovieTrack;

		foreach ( var block in track.Blocks )
		{
			result.Add( block );
		}

		foreach ( var preview in TrackWidget.TrackList.Session.EditMode?.GetPreviewBlocks( track ) ?? [] )
		{
			result.Add( preview );
		}
	}

	public void UpdateBlockItems()
	{
		if ( Visible && _canCreatePreview is not false )
		{
			var session = TrackWidget.TrackList.Session;

			_blocks.Clear();
			GetBlocks( _blocks );

			while ( _blockItems.Count > _blocks.Count )
			{
				_blockItems[^1].Destroy();
				_blockItems.RemoveAt( _blockItems.Count - 1 );
			}

			for ( var i = 0; i < _blocks.Count; ++i )
			{
				var block = _blocks[i];

				if ( _blockItems.Count <= i )
				{
					if ( BlockItem.Create( this, _blocks[i] ) is not { } newPreview )
					{
						_canCreatePreview = false;
						return;
					}

					_blockItems.Add( newPreview );
				}

				var preview = BlockItems[i];

				preview.Block = block;
				preview.Layout();
			}
		}
		else
		{
			ClearBlockItems();
		}
	}
}
