
using Sandbox.Diagnostics;
using Sandbox.UI;

namespace Editor.MovieMaker;

#nullable enable

public sealed class ProjectNavigationWidget : Widget
{
	private ProjectNavigationWidget? _child;

	public TrackListWidget TrackList { get; }

	protected override Vector2 SizeHint() => 32f + (_child is { } child ? child.SizeHint() : 0f);

	public ProjectNavigationWidget( TrackListWidget trackList, Session session )
		: base( trackList )
	{
		TrackList = trackList;

		Layout = Layout.Column();
		Layout.Margin = 0f;
		Layout.Spacing = 0f;

		VerticalSizeMode = SizeMode.CanGrow;

		var titleRow = Layout.AddRow();

		titleRow.Spacing = 4f;
		titleRow.Margin = new Margin( 12f, 4f );
		titleRow.Add( new Label( session.Title ) );
		titleRow.AddStretchCell();
	}

	public void SetChild( ProjectNavigationWidget child )
	{
		Assert.IsNull( _child );

		var childContainer = Layout.Add( Layout.Column() );

		childContainer.Margin = new Margin( 8f, 0f, 0f, 0f );
		childContainer.Spacing = 0f;
		childContainer.Add( child );

		_child = child;
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = false;
		Paint.SetBrushAndPen( Theme.Primary );
		Paint.DrawRect( new Rect( LocalRect.Left + 1f, LocalRect.Top + 1f, LocalRect.Width - 2f, Timeline.TrackHeight - 2f ), 4 );
	}
}
