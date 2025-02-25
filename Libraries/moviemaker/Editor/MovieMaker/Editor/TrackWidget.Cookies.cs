using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class TrackWidget
{
	private string CookiePrefix => $"{Session.CookiePrefix}.track.{ProjectTrack.Id}";

	public T GetCookie<T>( string key, T fallback )
	{
		return ProjectCookie.Get( $"{CookiePrefix}.{key}", fallback );
	}

	public void SetCookie<T>( string key, T value )
	{
		ProjectCookie.Set( $"{CookiePrefix}.{key}", value );
	}

	public class CookieHelper( TrackWidget trackWidget )
	{
		public bool IsLocked
		{
			get => trackWidget.GetCookie( nameof(IsLocked), true );
			set => trackWidget.SetCookie( nameof(IsLocked), value );
		}

		public bool IsCollapsed
		{
			get => trackWidget.GetCookie( nameof(IsCollapsed), true );
			set => trackWidget.SetCookie( nameof(IsCollapsed), value );
		}
	}

	private CookieHelper? _cookieHelper;
	public CookieHelper Cookies => _cookieHelper ??= new CookieHelper( this );

	public void RestoreFromCookies()
	{
		_isLocked = Cookies.IsLocked;
	}
}
