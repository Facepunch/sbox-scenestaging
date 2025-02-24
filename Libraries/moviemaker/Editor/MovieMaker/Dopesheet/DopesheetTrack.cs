namespace Editor.MovieMaker;

#nullable enable

public partial class DopeSheetTrack : GraphicsItem
{
	public TrackWidget TrackWidget { get; }

	private bool? _canCreatePreview;

	private List<BlockPreview> BlockPreviews { get; } = new();

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
		if ( BlockPreviews.Count == 0 ) return;

		foreach ( var blockPreview in BlockPreviews )
		{
			blockPreview.Destroy();
		}

		BlockPreviews.Clear();
	}

	internal void OnSelected()
	{
		if ( TrackWidget.Property?.GetTargetGameObject() is { } gameObject )
		{
			SceneEditorSession.Active.Selection.Set( gameObject );
		}
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
			if ( TrackWidget.MovieTrack.Blocks.Count != BlockPreviews.Count )
			{
				ClearBlockPreviews();
			}

			var blocks = TrackWidget.MovieTrack.Blocks;

			for ( var i = 0; i < blocks.Count; ++i )
			{
				var block = blocks[i];

				if ( BlockPreviews.Count <= i )
				{
					if ( BlockPreview.Create( this, block ) is not { } newPreview )
					{
						_canCreatePreview = false;
						return;
					}

					BlockPreviews.Add( newPreview );
				}

				var preview = BlockPreviews[i];
				var duration = block.Duration ?? TrackWidget.MovieTrack.Clip.Duration - block.StartTime;

				preview.Block = block;
				preview.PrepareGeometryChange();
				preview.Position = new Vector2( Session.Current.TimeToPixels( block.StartTime ), 0f );
				preview.Size = new Vector2( Session.Current.TimeToPixels( duration ), LocalRect.Height );

				preview.Update();
			}
		}
		else
		{
			ClearBlockPreviews();
		}
	}
}
