using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public record EditModeType( TypeDescription TypeDescription )
{
	public string Title => TypeDescription.Title;
	public string Description => TypeDescription.Description;
	public string Icon => TypeDescription.Icon;
	public int Order => TypeDescription.Order;

	public EditMode Create()
	{
		return TypeDescription.Create<EditMode>();
	}

	public bool IsMatchingType( EditMode? editMode )
	{
		return TypeDescription.TargetType == editMode?.GetType();
	}
}

public abstract class EditMode
{
	protected readonly struct ToolbarHelper( Layout toolbar )
	{
		public IconButton AddToggle( string title, string icon, Func<bool> getState, Action<bool> setState )
		{
			var btn = new IconButton( icon )
			{
				ToolTip = title,
				IconSize = 16,
				IsToggle = true,
				Background = Color.Transparent,
				BackgroundActive = Color.Transparent,
				ForegroundActive = Theme.Primary
			};

			btn.Bind( "IsActive" ).From( getState, setState );

			toolbar.Add( btn );

			return btn;
		}

		public IconButton AddToggle( InterpolationMode value, Func<bool> getState, Action<bool> setState )
		{
			var entry = EditorTypeLibrary.GetEnumDescription( typeof(InterpolationMode) )
				.FirstOrDefault( x => x.IntegerValue == (long)value );

			var btn = new InterpolationButton( value )
			{
				ToolTip = entry.Title ?? value.ToString().ToTitleCase(),
				IconSize = 14f,
				IsToggle = true,
				ForegroundActive = Theme.Primary
			};

			btn.Bind( "IsActive" ).From( getState, setState );

			toolbar.Add( btn );

			return btn;
		}

		public void AddSpacingCell() => toolbar.AddSpacingCell( 16f );
	}

	protected Session Session { get; private set; } = null!;
	protected DopeSheet DopeSheet { get; private set; } = null!;
	protected ToolbarHelper Toolbar { get; private set; }

	protected IEnumerable<GraphicsItem> SelectedItems => DopeSheet.SelectedItems;
	protected TrackListWidget TrackList => Session.Editor.TrackList;

	/// <summary>
	/// Can we create new tracks when properties are edited in the scene?
	/// </summary>
	public virtual bool AllowTrackCreation => false;

	internal void Enable( Session session )
	{
		Session = session;
		DopeSheet = session.Editor.TrackList.DopeSheet;
		Toolbar = new( Session.Editor.Toolbar.EditModeControls );

		EditorShortcuts.Register( this, DopeSheet );

		OnEnable();

		foreach ( var track in DopeSheet.Items.OfType<DopeSheetTrack>() )
		{
			OnTrackAdded( track );
		}
	}

	protected virtual void OnEnable() { }

	internal void Disable()
	{
		foreach ( var track in DopeSheet.Items.OfType<DopeSheetTrack>() )
		{
			OnTrackRemoved( track );
		}

		OnDisable();

		EditorShortcuts.Unregister( this );

		Session = null!;
	}

	protected virtual void OnDisable() { }

	internal bool PreChange( MovieTrack track )
	{
		if ( TrackList.Tracks.FirstOrDefault( x => x.MovieTrack == track )?.DopeSheetTrack is { } channel )
		{
			return OnPreChange( channel );
		}

		return false;
	}

	protected virtual bool OnPreChange( DopeSheetTrack track ) => false;

	internal bool PostChange( MovieTrack track )
	{
		if ( TrackList.Tracks.FirstOrDefault( x => x.MovieTrack == track )?.DopeSheetTrack is { } channel )
		{
			return OnPostChange( channel );
		}

		return false;
	}

	protected virtual bool OnPostChange( DopeSheetTrack track ) => false;

	#region UI Events

	internal void MousePress( MouseEvent e ) => OnMousePress( e );
	protected virtual void OnMousePress( MouseEvent e ) { }
	internal void MouseRelease( MouseEvent e ) => OnMouseRelease( e );
	protected virtual void OnMouseRelease( MouseEvent e ) { }
	internal void MouseMove( MouseEvent e ) => OnMouseMove( e );
	protected virtual void OnMouseMove( MouseEvent e ) { }
	internal void MouseWheel( WheelEvent e ) => OnMouseWheel( e );
	protected virtual void OnMouseWheel( WheelEvent e ) { }

	internal void KeyPress( KeyEvent e ) => OnKeyPress( e );
	protected virtual void OnKeyPress( KeyEvent e ) { }
	internal void KeyRelease( KeyEvent e ) => OnKeyRelease( e );
	protected virtual void OnKeyRelease( KeyEvent e ) { }

	internal void Copy() => OnCopy();
	protected virtual void OnCopy() { }

	internal void Paste() => OnPaste();
	protected virtual void OnPaste() { }

	internal void Delete() => OnDelete();
	protected virtual void OnDelete() { }

	internal void TrackAdded( DopeSheetTrack track ) => OnTrackAdded( track );
	protected virtual void OnTrackAdded( DopeSheetTrack track ) { }

	internal void TrackRemoved( DopeSheetTrack track ) => OnTrackRemoved( track );
	protected virtual void OnTrackRemoved( DopeSheetTrack track ) { }

	internal void TrackLayout( DopeSheetTrack track, Rect rect ) => OnTrackLayout( track, rect );
	protected virtual void OnTrackLayout( DopeSheetTrack track, Rect rect ) { }

	internal void ScrubberPaint( ScrubberWidget scrubber ) => OnScrubberPaint( scrubber );
	protected virtual void OnScrubberPaint( ScrubberWidget scrubber ) { }

	#endregion

	public static IReadOnlyList<EditModeType> AllTypes => EditorTypeLibrary.GetTypes<EditMode>()
		.Where( x => !x.IsAbstract )
		.Select( x => new EditModeType( x ) )
		.OrderBy( x => x.Order )
		.ToArray();

	public static EditModeType DefaultType => AllTypes.First();
}
