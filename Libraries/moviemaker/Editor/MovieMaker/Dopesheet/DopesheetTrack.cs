using Editor.MovieMaker.BlockPreviews;

namespace Editor.MovieMaker;

#nullable enable

public partial class DopeSheetTrack : GraphicsItem
{
	public TrackWidget TrackWidget { get; }

	private bool? _canCreatePreview;

	private readonly List<BlockPreview> _blockPreviews = new();

	public IReadOnlyList<BlockPreview> BlockPreviews => _blockPreviews;

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

		UpdateBlockPreviews();

		TrackWidget.TrackList.Session.EditMode?.TrackLayout( this, r );
	}

	private void ClearBlockPreviews()
	{
		if ( _blockPreviews.Count == 0 ) return;

		foreach ( var blockPreview in _blockPreviews )
		{
			blockPreview.Destroy();
		}

		_blockPreviews.Clear();
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

	public void UpdateBlockPreviews()
	{
		if ( Visible && _canCreatePreview is not false )
		{
			var session = TrackWidget.TrackList.Session;

			if ( TrackWidget.MovieTrack.Blocks.Count != _blockPreviews.Count )
			{
				ClearBlockPreviews();
			}

			var blocks = TrackWidget.MovieTrack.Blocks;

			for ( var i = 0; i < blocks.Count; ++i )
			{
				var block = blocks[i];

				if ( _blockPreviews.Count <= i )
				{
					if ( BlockPreview.Create( this, block ) is not { } newPreview )
					{
						_canCreatePreview = false;
						return;
					}

					_blockPreviews.Add( newPreview );
				}

				var preview = BlockPreviews[i];
				var duration = block.Duration;

				preview.Block = block;
				preview.PrepareGeometryChange();
				preview.Position = new Vector2( session.TimeToPixels( block.StartTime ), 0f );
				preview.Size = new Vector2( session.TimeToPixels( duration ), LocalRect.Height );

				preview.Update();
			}
		}
		else
		{
			ClearBlockPreviews();
		}
	}
}
