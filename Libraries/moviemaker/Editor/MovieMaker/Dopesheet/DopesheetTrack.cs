using Editor.MovieMaker.BlockDisplays;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public partial class DopeSheetTrack : GraphicsItem
{
	public Session Session { get; }
	public ITrackView View { get; }

	private bool? _canCreateItem;

	private readonly List<(IPropertyBlock Block, MovieTime Offset)> _blocks = new();
	private readonly List<BlockItem> _blockItems = new();

	public IReadOnlyList<BlockItem> BlockItems => _blockItems;

	public DopeSheetTrack( DopeSheet dopeSheet, ITrackView view )
	{
		Session = dopeSheet.Session;
		View = view;

		HoverEvents = true;

		View.ValueChanged += View_ValueChanged;
	}

	protected override void OnDestroy()
	{
		View.ValueChanged -= View_ValueChanged;
	}

	private void View_ValueChanged( ITrackView view )
	{
		UpdateBlockItems();
	}

	internal void UpdateLayout()
	{
		PrepareGeometryChange();

		var position = View.Position;

		Position = new Vector2( 0, position + 1f );
		Size = new Vector2( 50000, DopeSheet.TrackHeight - 2f );

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

	private void GetBlocks( List<(IPropertyBlock Block, MovieTime Offset)> result )
	{
		if ( View.Track is not IProjectPropertyTrack propertyTrack ) return;

		foreach ( var block in propertyTrack.Blocks )
		{
			result.Add( (block, default) );
		}

		foreach ( var preview in Session.EditMode?.GetPreviewBlocks( propertyTrack ) ?? [] )
		{
			result.Add( (preview, Session.EditMode!.PreviewBlockOffset) );
		}
	}

	public void UpdateBlockItems()
	{
		if ( _canCreateItem is false )
		{
			ClearBlockItems();
			return;
		}

		_blocks.Clear();
		GetBlocks( _blocks );

		while ( _blockItems.Count > _blocks.Count )
		{
			_blockItems[^1].Destroy();
			_blockItems.RemoveAt( _blockItems.Count - 1 );
		}

		for ( var i = 0; i < _blocks.Count; ++i )
		{
			var (block, offset) = _blocks[i];

			if ( _blockItems.Count <= i )
			{
				if ( BlockItem.Create( this, block, offset ) is not { } newPreview )
				{
					_canCreateItem = false;
					return;
				}

				_blockItems.Add( newPreview );
			}

			var item = BlockItems[i];

			item.Block = block;
			item.Offset = offset;
			item.Layout();
		}
	}
}
