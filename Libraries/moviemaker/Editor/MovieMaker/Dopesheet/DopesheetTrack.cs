using System.Collections.Generic;
using System.Linq;
using Editor.MovieMaker.BlockDisplays;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public partial class DopeSheetTrack : GraphicsItem
{
	public DopeSheet DopeSheet { get; }
	public Session Session { get; }
	public ITrackView View { get; }

	private bool? _canCreateItem;

	private readonly List<(ITrackBlock Block, MovieTime? Offset)> _visibleBlocks = new();
	private readonly List<BlockItem> _blockItems = new();

	public IReadOnlyList<BlockItem> BlockItems => _blockItems;

	public DopeSheetTrack( DopeSheet dopeSheet, ITrackView view )
	{
		DopeSheet = dopeSheet;
		Session = dopeSheet.Session;
		View = view;

		HoverEvents = true;
		ToolTip = view.Description;

		View.Changed += View_Changed;
		View.ValueChanged += View_ValueChanged;
	}

	protected override void OnDestroy()
	{
		View.Changed -= View_Changed;
		View.ValueChanged -= View_ValueChanged;
	}

	private void View_Changed( ITrackView view )
	{
		UpdateBlockItems();
	}

	private void View_ValueChanged( ITrackView view )
	{
		UpdateBlockItems();
	}

	internal void UpdateLayout()
	{
		PrepareGeometryChange();

		var position = View.Position;

		Position = new Vector2( 0, position );
		Size = new Vector2( 50000, DopeSheet.TrackHeight );

		UpdateBlockItems();
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
		View.InspectProperty();
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		base.OnMousePressed( e );

		if ( e.LeftMouseButton )
		{
			OnSelected();
		}
	}

	public void UpdateBlockItems()
	{
		var previewOffset = Session.TrackList.PreviewOffset;

		_visibleBlocks.Clear();
		_visibleBlocks.AddRange( View.Blocks.Select( x => (x, (MovieTime?)null) ) );
		_visibleBlocks.AddRange( View.PreviewBlocks.Select( x => (x, (MovieTime?)previewOffset) ) );

		if ( _canCreateItem is false )
		{
			ClearBlockItems();
			return;
		}

		while ( _blockItems.Count > _visibleBlocks.Count )
		{
			_blockItems[^1].Destroy();
			_blockItems.RemoveAt( _blockItems.Count - 1 );
		}

		for ( var i = 0; i < _visibleBlocks.Count; ++i )
		{
			var (block, offset) = _visibleBlocks[i];

			if ( _blockItems.Count <= i )
			{
				if ( BlockItem.Create( this, block, offset ?? default ) is not { } newPreview )
				{
					_canCreateItem = false;
					return;
				}

				_blockItems.Add( newPreview );
			}

			var item = BlockItems[i];

			item.Block = block;
			item.Offset = offset ?? default;
			item.Layout();
		}
	}
}
