
namespace Editor.MovieMaker;

#nullable enable

public sealed class ParentProjectWidget : Widget
{
	public TrackListWidget TrackList { get; }

	protected override Vector2 SizeHint() => 32f;

	public ParentProjectWidget( TrackListWidget trackList, Session parentSession )
		: base( trackList )
	{
		TrackList = trackList;

		Layout = Layout.Row();

		var label = Layout.Add( new Label( parentSession.Title ) );

		Layout.AddStretchCell();
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = false;
		Paint.SetBrushAndPen( Theme.Primary );
		Paint.DrawRect( new Rect( LocalRect.Left + 1f, LocalRect.Top + 1f, LocalRect.Width - 2f, Timeline.TrackHeight - 2f ), 4 );
	}
}
