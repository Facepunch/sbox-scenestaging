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
	}

	private CookieHelper? _cookieHelper;
	public CookieHelper Cookies => _cookieHelper ??= new CookieHelper( this );
}
