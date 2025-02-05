
using Sandbox.UI;

namespace Editor.MovieMaker;

#nullable enable


public class TrackGroup : Widget
{
	private bool _collapsed;

	public bool Collapsed
	{
		get => _collapsed;
		set
		{
			_collapsed = value;
			CollapseButton.Update();

			foreach ( var child in Children )
			{
				if ( child == Header ) continue;

				if ( child is TrackWidget or TrackGroup )
				{
					child.Hidden = _collapsed;
				}
			}

			Header.TrackList.DopeSheet.UpdateTracks();
		}
	}

	public TrackWidget Header { get; }
	public Layout Content { get; set; }

	public Button CollapseButton { get; }

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
		Paint.SetBrushAndPen( Extensions.PaintSelectColor( DopeSheet.Colors.Background,
			Theme.ControlBackground.Lighten( 0.5f ), Theme.Primary ) );
		Paint.DrawRect( LocalRect, 4f );

		Paint.SetPen( Theme.ControlText );
		Paint.DrawIcon( LocalRect, Group.Collapsed ? "add" : "remove", 12f );
	}

	protected override void OnClicked()
	{
		Group.Collapsed = !Group.Collapsed;
	}
}
