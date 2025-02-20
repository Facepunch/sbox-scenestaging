using Editor.MovieMaker.BlockPreviews;
using Sandbox.MovieMaker;

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

	private void GetBlocks( List<IMovieBlock> result )
	{
		foreach ( var block in TrackWidget.MovieTrack.Blocks )
		{
			result.Add( block );
		}

		foreach ( var preview in TrackWidget.TrackList.Session.EditMode?.GetPreviewBlocks() ?? [] )
		{
			if ( preview.Track == TrackWidget.MovieTrack )
			{
				result.Add( preview );
			}
		}
	}

	private readonly List<IMovieBlock> _blocks = new();

	public void UpdateBlockPreviews()
	{
		if ( Visible && _canCreatePreview is not false )
		{
			var session = TrackWidget.TrackList.Session;

			_blocks.Clear();
			GetBlocks( _blocks );

			if ( _blocks.Count != _blockPreviews.Count )
			{
				ClearBlockPreviews();
			}

			for ( var i = 0; i < _blocks.Count; ++i )
			{
				var block = _blocks[i];

				if ( _blockPreviews.Count <= i )
				{
					if ( BlockPreview.Create( this, _blocks[i] ) is not { } newPreview )
					{
						_canCreatePreview = false;
						return;
					}

					_blockPreviews.Add( newPreview );
				}

				var preview = BlockPreviews[i];

				preview.Block = block;

				preview.PrepareGeometryChange();
				preview.Position = new Vector2( session.TimeToPixels( block.TimeRange.Start ), 0f );
				preview.Size = new Vector2( session.TimeToPixels( block.TimeRange.Duration ), LocalRect.Height );

				preview.Update();
			}
		}
		else
		{
			ClearBlockPreviews();
		}
	}
}
