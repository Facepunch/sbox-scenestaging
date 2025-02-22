using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class Session
{
	private string CookiePrefix => $"moviemaker.{Player.ReferencedClip?.ResourceId.ToString() ?? Player.Id.ToString()}";

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
		FrameSnap = Cookies.FrameSnap;
		ObjectSnap = Cookies.ObjectSnap;
		TimeOffset = Cookies.TimeOffset;
		PixelsPerSecond = Cookies.PixelsPerSecond;

		SmoothPan.Target = SmoothPan.Value = (float)TimeOffset.TotalSeconds;
		SmoothZoom.Target = SmoothZoom.Value = PixelsPerSecond;

		SetEditMode( Cookies.EditMode );
	}
}
