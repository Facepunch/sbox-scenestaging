using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

public class TrackGroup : Widget
{
	public TrackWidget Header { get; }
	public Layout Content { get; set; }

	public Button CollapseButton { get; }

	public new IEnumerable<TrackWidget> Children => base.Children
		.OfType<TrackWidget>()
		.Where( x => x != Header );

	public TrackGroup( TrackWidget header ) : base()
	{
		Header = header;
		MinimumSize = 32;

		Layout = Layout.Column();
		Layout.Spacing = 2f;
		Layout.Margin = new Sandbox.UI.Margin( 0f );
		Layout.Add( header );

		Content = Layout.AddColumn();
		Content.Spacing = 2;
		Content.Margin = new Sandbox.UI.Margin( 6f, 0f, 0f, 0f );

		CollapseButton = new CollapseButton( this );

		header.Buttons.Add( CollapseButton );
	}

	public void UpdateCollapsedState()
	{
		CollapseButton.Update();

		foreach ( var child in Children )
		{
			child.Hidden = Header.Hidden;
		}

		Header.TrackList.DopeSheet.UpdateTracks();
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = false;
		Paint.SetBrushAndPen( Header.BackgroundColor );
		Paint.DrawRect( new Rect( LocalRect.Left, LocalRect.Top + 8f, 4f, LocalRect.Height - 8f ), 2f );
	}
}

file class CollapseButton : Button
{
	public TrackGroup Group { get; }

	public CollapseButton( TrackGroup group )
	{
		Group = group;

		FixedSize = 20f;
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( PaintExtensions.PaintSelectColor( DopeSheet.Colors.Background,
			Theme.ControlBackground.Lighten( 0.5f ), Theme.Primary ) );
		Paint.DrawRect( LocalRect, 4f );

		Paint.SetPen( Theme.ControlText );
		Paint.DrawIcon( LocalRect, Group.Header.IsCollapsed ? "add" : "remove", 12f );
	}

	protected override void OnClicked()
	{
		Group.Header.IsCollapsed = !Group.Header.IsCollapsed;
	}
}
