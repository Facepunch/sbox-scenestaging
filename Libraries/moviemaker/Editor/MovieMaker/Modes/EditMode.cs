using System.Globalization;
using System.Linq;
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

		public (FloatSlider Slider, Label Label) AddSlider( string title, Func<float> getValue, Action<float> setValue, float minimum = 0f,
			float maximum = 1f, float step = 0.01f, Func<string>? getLabel = null )
		{
			var slider = new FloatSlider( null )
			{
				ToolTip = title,
				FixedWidth = 80f,
				Minimum = minimum,
				Maximum = maximum,
				Step = step
			};

			slider.Bind( nameof( FloatSlider.Value ) )
				.From( getValue, setValue );

			toolbar.Add( slider );

			var label = new Label( null )
			{
				Color = Color.White.Darken( 0.5f ),
				Margin = 4f,
				Alignment = TextFlag.Left
			};

			label.Bind( nameof( Label.Text ) )
				.ReadOnly()
				.From( getLabel ?? (() => slider.Value.ToString( CultureInfo.InvariantCulture )), (Action<string>?) null );

			toolbar.Add( label );

			return (slider, label);
		}

		public void AddSpacingCell() => toolbar.AddSpacingCell( 16f );
	}

	public Session Session { get; private set; } = null!;
	public MovieProject Project => Session.Project;
	protected DopeSheet DopeSheet { get; private set; } = null!;
	protected ToolbarHelper Toolbar { get; private set; }

	public MovieTimeRange? PasteTimeRange { get; protected set; }

	protected IEnumerable<GraphicsItem> SelectedItems => DopeSheet.SelectedItems;
	protected TrackListWidget TrackList => Session.Editor.TrackList;

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
		DopeSheet = session.Editor.TrackList.DopeSheet;
		Toolbar = new( Session.Editor.Toolbar.EditModeControls );

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

		Session = null!;
	}

	protected virtual void OnDisable() { }

	internal bool StartRecording() => OnStartRecording();
	protected virtual bool OnStartRecording() => false;

	internal void StopRecording() => OnStopRecording();
	protected virtual void OnStopRecording() { }

	internal void Frame() => OnFrame();
	protected virtual void OnFrame() { }

	internal bool PreChange( IProjectTrack track )
	{
		if ( Session.Binder.Get( track ) is not ITrackProperty { CanWrite: true } ) return false;

		var trackWidget = TrackList.Tracks.FirstOrDefault( x => x.ProjectTrack == track );
		if ( trackWidget is not { CanEdit: true } ) return false;

		if ( trackWidget.DopeSheetTrack is { } channel )
		{
			return OnPreChange( channel );
		}

		return false;
	}

	protected virtual bool OnPreChange( DopeSheetTrack track ) => false;

	internal bool PostChange( IProjectTrack track )
	{
		if ( Session.Binder.Get( track ) is not ITrackProperty { CanWrite: true } ) return false;

		var trackWidget = TrackList.Tracks.FirstOrDefault( x => x.ProjectTrack == track );
		if ( trackWidget is not { CanEdit: true } ) return false;

		if ( trackWidget.DopeSheetTrack is { } channel )
		{
			return OnPostChange( channel );
		}

		return false;
	}

	protected virtual bool OnPostChange( DopeSheetTrack track ) => false;

	protected DopeSheetTrack? GetTrackAt( Vector2 scenePos )
	{
		return DopeSheet.Items
			.OfType<DopeSheetTrack>()
			.FirstOrDefault( x => x.SceneRect.IsInside( scenePos ) );
	}

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

	internal void Delete( bool shift ) => OnDelete( shift );
	protected virtual void OnDelete( bool shift ) { }

	internal void Insert() => OnInsert();
	protected virtual void OnInsert() { }

	internal void TrackAdded( DopeSheetTrack track ) => OnTrackAdded( track );
	protected virtual void OnTrackAdded( DopeSheetTrack track ) { }

	internal void TrackRemoved( DopeSheetTrack track ) => OnTrackRemoved( track );
	protected virtual void OnTrackRemoved( DopeSheetTrack track ) { }

	internal void TrackStateChanged( DopeSheetTrack track ) => OnTrackStateChanged( track );
	protected virtual void OnTrackStateChanged( DopeSheetTrack track ) { }

	internal void TrackLayout( DopeSheetTrack track, Rect rect ) => OnTrackLayout( track, rect );
	protected virtual void OnTrackLayout( DopeSheetTrack track, Rect rect ) { }

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
				if ( block.TimeRange.Contains( time ) && Session.Binder.Get( track ) is {  } target )
				{
					target.Value = block.GetValue( time );
				}
			}
		}
	}

	public void SetPreviewBlocks( IProjectPropertyTrack track, IEnumerable<IPropertyBlock> blocks )
	{
		if ( !_previewBlocks.TryGetValue( track, out var list ) )
		{
			_previewBlocks.Add( track, list = new List<IPropertyBlock>() );
		}

		list.Clear();
		list.AddRange( blocks );

		if ( TrackList.FindTrack( track ) is { } trackWidget )
		{
			trackWidget.NoteInteraction();
			trackWidget.DopeSheetTrack?.UpdateBlockItems();
		}
	}

	public void ClearPreviewBlocks( IProjectPropertyTrack track )
	{
		_previewBlocks.Remove( track );

		if ( TrackList.FindTrack( track ) is { } trackWidget )
		{
			trackWidget.DopeSheetTrack?.UpdateBlockItems();
		}
	}

	public IEnumerable<IPropertyBlock> GetPreviewBlocks( IProjectPropertyTrack track )
	{
		return _previewBlocks.GetValueOrDefault( track, [] );
	}
}
