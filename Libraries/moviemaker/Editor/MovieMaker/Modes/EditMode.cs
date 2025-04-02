using System.Globalization;
using System.Linq;
using System.Threading.Channels;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public record EditModeType( TypeDescription TypeDescription )
{
	public string Name => TypeDescription.Name;
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

public abstract partial class EditMode
{
	protected static EditMode? Focused
	{
		get
		{
			var widget = Application.FocusWidget;

			while ( widget != null )
			{
				if ( widget is MovieEditor editor ) return editor.Session?.EditMode;
				widget = widget.Parent;
			}

			return null;
		}
	}

	public Session Session { get; private set; } = null!;
	public MovieProject Project => Session.Project;
	protected DopeSheet DopeSheet { get; private set; } = null!;
	protected ToolbarWidget Toolbar { get; private set; } = null!;

	public MovieTimeRange? SourceTimeRange { get; protected set; }

	/// <summary>
	/// Can we create new tracks when properties are edited in the scene?
	/// </summary>
	public virtual bool AllowTrackCreation => false;

	/// <summary>
	/// Can we start / stop recording?
	/// </summary>
	public virtual bool AllowRecording => false;

	internal void Enable( Session session )
	{
		Session = session;
		DopeSheet = session.Editor.DopeSheetPanel!.DopeSheet;
		Toolbar = session.Editor.DopeSheetPanel!.ToolBar;

		OnEnable();
	}

	protected virtual void OnEnable() { }

	internal void Disable()
	{
		OnDisable();

		Session = null!;
	}

	protected virtual void OnDisable() { }

	internal bool StartRecording() => OnStartRecording();
	protected virtual bool OnStartRecording() => false;

	internal void StopRecording() => OnStopRecording();
	protected virtual void OnStopRecording() { }

	internal void Frame() => OnFrame();
	protected virtual void OnFrame() { }

	internal bool PreChange( ITrackView view ) => OnPreChange( view );

	protected virtual bool OnPreChange( ITrackView track ) => false;

	internal bool PostChange( ITrackView view ) => OnPostChange( view );

	protected virtual bool OnPostChange( ITrackView track ) => false;

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
	internal void SelectAll() => OnSelectAll();
	protected virtual void OnSelectAll() { }

	internal void Cut() => OnCut();
	protected virtual void OnCut() { }

	internal void Copy() => OnCopy();
	protected virtual void OnCopy() { }

	internal void Paste() => OnPaste();
	protected virtual void OnPaste() { }

	internal void Backspace() => OnBackspace();
	protected virtual void OnBackspace() { }

	internal void Delete() => OnDelete();
	protected virtual void OnDelete() { }

	internal void Insert() => OnInsert();
	protected virtual void OnInsert() { }

	internal void TrackStateChanged( ITrackView view ) => OnTrackStateChanged( view );
	protected virtual void OnTrackStateChanged( ITrackView view ) { }

	internal void ViewChanged( Rect viewRect ) => OnViewChanged( viewRect );
	protected virtual void OnViewChanged( Rect viewRect ) { }

	#endregion

	public static IReadOnlyList<EditModeType> AllTypes => EditorTypeLibrary.GetTypes<EditMode>()
		.Where( x => !x.IsAbstract )
		.Select( x => new EditModeType( x ) )
		.OrderBy( x => x.Order )
		.ToArray();

	public static EditModeType Get( string name ) => AllTypes.FirstOrDefault( x => x.TypeDescription.Name == name )
		?? AllTypes.First();

	public void GetSnapTimes( ref TimeSnapHelper snapHelper ) => OnGetSnapTimes( ref snapHelper );

	protected virtual void OnGetSnapTimes( ref TimeSnapHelper snapHelper ) { }

	public void ApplyFrame( MovieTime time )
	{
		OnApplyFrame( time );
	}

	private readonly Dictionary<IProjectPropertyTrack, List<IPropertyBlock>> _previewBlocks = new();

	protected virtual void OnApplyFrame( MovieTime time )
	{
		foreach ( var (track, list) in _previewBlocks )
		{
			foreach ( var block in list )
			{
				if ( !block.TimeRange.Contains( time ) ) continue;
				if ( Session.Binder.Get( track ) is not { } target ) continue;

				target.Value = block.GetValue( time );
			}
		}
	}

	public void SetPreviewBlocks( IProjectPropertyTrack track, IEnumerable<IPropertyBlock> blocks, MovieTime offset = default )
	{
		if ( !_previewBlocks.TryGetValue( track, out var list ) )
		{
			_previewBlocks.Add( track, list = new List<IPropertyBlock>() );
		}

		PreviewBlockOffset = offset;

		list.Clear();
		list.AddRange( blocks );

		if ( Session.TrackList.Find( track ) is { } view )
		{
			view.MarkValueChanged();
		}
	}

	public void ClearPreviewBlocks( IProjectPropertyTrack track )
	{
		PreviewBlockOffset = default;

		_previewBlocks.Remove( track );

		if ( Session.TrackList.Find( track ) is { } view )
		{
			view.MarkValueChanged();
		}
	}

	public MovieTime PreviewBlockOffset { get; private set; }

	public IEnumerable<IPropertyBlock> GetPreviewBlocks( IProjectPropertyTrack track )
	{
		return _previewBlocks.GetValueOrDefault( track, [] );
	}
}
