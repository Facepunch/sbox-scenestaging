using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class Session
{
	internal string CookiePrefix => $"moviemaker.{(Player.Resource as MovieResource)?.ResourceId.ToString() ?? Player.Id.ToString()}";

	public T GetCookie<T>( string key, T fallback )
	{
		return ProjectCookie.Get( $"{CookiePrefix}.{key}", fallback );
	}

	public void SetCookie<T>( string key, T value )
	{
		ProjectCookie.Set( $"{CookiePrefix}.{key}", value );
	}

	public class CookieHelper( Session session )
	{
		public EditModeType EditMode
		{
			get => MovieMaker.EditMode.Get( session.GetCookie( nameof(EditMode), "" ) );
			set => session.SetCookie( nameof(EditMode), value.Name );
		}

		public bool IsLooping
		{
			get => session.GetCookie( nameof(IsLooping), true );
			set => session.SetCookie( nameof(IsLooping), value );
		}

		public float TimeScale
		{
			get => session.GetCookie( nameof(TimeScale), 1f );
			set => session.SetCookie( nameof(TimeScale), value );
		}

		public int FrameRate
		{
			get => session.GetCookie( nameof( FrameRate ), 10 );
			set => session.SetCookie( nameof( FrameRate ), value );
		}

		public bool FrameSnap
		{
			get => session.GetCookie( nameof(FrameSnap), true );
			set => session.SetCookie( nameof(FrameSnap), value );
		}

		public bool ObjectSnap
		{
			get => session.GetCookie( nameof(ObjectSnap), true );
			set => session.SetCookie( nameof(ObjectSnap), value );
		}

		public MovieTime TimeOffset
		{
			get => MovieTime.FromTicks( session.GetCookie( nameof(TimeOffset), 0 ) );
			set => session.SetCookie( nameof(TimeOffset), value.Ticks );
		}

		public float PixelsPerSecond
		{
			get => session.GetCookie( nameof( PixelsPerSecond ), 100f );
			set => session.SetCookie( nameof( PixelsPerSecond ), value );
		}
	}

	private CookieHelper? _cookieHelper;
	public CookieHelper Cookies => _cookieHelper ??= new CookieHelper( this );

	public void RestoreFromCookies()
	{
		if ( IsEditorScene )
		{
			_isLooping = Cookies.IsLooping;
			_timeScale = Cookies.TimeScale;
		}

		_frameRate = Cookies.FrameRate;
		_frameSnap = Cookies.FrameSnap;
		_objectSnap = Cookies.ObjectSnap;

		SetView( Cookies.TimeOffset, Cookies.PixelsPerSecond );
		SetEditMode( Cookies.EditMode );
	}
}
