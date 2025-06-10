namespace Editor.MovieMaker;

#nullable enable

partial class Session
{
	private TrackListView? _trackList;
	private float _trackListScrollPosition;

	/// <summary>
	/// Which tracks should be visible in the track list / dope sheet.
	/// </summary>
	public TrackListView TrackList => _trackList ??= new TrackListView( this );

	public float TrackListScrollOffset => 32f;

	public float TrackListScrollPosition
	{
		get
		{
			var viewHeight = TrackListViewHeight;
			var contentsHeight = TrackList.Height;
			var headerOffset = -TrackListHeaderHeight + 28f;

			var max = Math.Max( headerOffset, contentsHeight - viewHeight );

			return Math.Clamp( _trackListScrollPosition, headerOffset, max );
		}
		set
		{
			if ( _trackListScrollPosition.Equals( value ) ) return;

			_trackListScrollPosition = value;
			Cookies.ScrollPosition = value;

			ViewChanged?.Invoke();
		}
	}

	public float TrackListHeaderHeight { get; set; } = 0f;
	public float TrackListViewHeight { get; set; } = float.PositiveInfinity;

	private void TrackFrame()
	{
		_trackList?.Frame();
	}
}
