using Editor.MovieMaker.BlockDisplays;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public partial class DopeSheetTrack : GraphicsItem
{
	public Session Session { get; }
	public IProjectPropertyTrack ProjectTrack { get; }
	public TrackWidget TrackWidget { get; }

	private bool? _canCreateItem;

	private readonly List<(IPropertyBlock Block, MovieTime Offset)> _blocks = new();
	private readonly List<BlockItem> _blockItems = new();

	public IReadOnlyList<BlockItem> BlockItems => _blockItems;

	public bool Visible => TrackWidget.Visible;

	public DopeSheetTrack( TrackWidget track, IProjectPropertyTrack propertyTrack )
	{
		Session = track.Session;
		ProjectTrack = propertyTrack;
		TrackWidget = track;
		HoverEvents = true;
	}

	internal void DoLayout( Rect r )
	{
		PrepareGeometryChange();

		Position = new Vector2( 0, r.Top + 1 );
		Size = Visible ? new Vector2( 50000, r.Height ) : 0f;

		UpdateBlockItems();

		Session.EditMode?.TrackLayout( this, r );
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

	private void GetBlocks( List<(IPropertyBlock Block, MovieTime Offset)> result )
	{
		foreach ( var block in ProjectTrack.Blocks )
		{
			result.Add( (block, default) );
		}

		foreach ( var preview in Session.EditMode?.GetPreviewBlocks( ProjectTrack ) ?? [] )
		{
			result.Add( (preview, Session.EditMode!.PreviewBlockOffset) );
		}
	}

	public void UpdateBlockItems()
	{
		if ( Visible && _canCreateItem is not false )
		{
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
		else
		{
			ClearBlockItems();
		}
	}
}
