

namespace Editor.MovieMaker;


public class TrackGroup : Widget
{
	public Layout Content { get; set; }

	public TrackGroup() : base()
	{
		MinimumSize = 32;

		Layout = Layout.Column();

		Content = Layout.AddColumn();
		Content.Spacing = 2;
		Content.Margin = new Sandbox.UI.Margin( 8, 4, 0, 4 );
	}

	protected override void OnPaint()
	{
	}
}

