using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Describes which tracks should be shown in the track list / dope sheet.
/// </summary>
public interface ITrackListView
{
	IReadOnlyList<ITrackView> RootTracks { get; }
	int StateHash { get; }

	/// <summary>
	/// Invoked when tracks are added or removed.
	/// </summary>
	event Action<ITrackListView> Changed;

	public IEnumerable<ITrackView> AllTracks => RootTracks.SelectMany( EnumerateDescendants );
	public IEnumerable<ITrackView> VisibleTracks => RootTracks.SelectMany( EnumerateVisibleDescendants );

	public IEnumerable<ITrackView> EditableTracks =>
		AllTracks.Where( x => x is { IsLocked: false, Target: ITrackProperty { CanWrite: true } } );

	public ITrackView? Find( IProjectTrack track ) => AllTracks.FirstOrDefault( x => x.Track == track );
	public ITrackView? FindEditable( IProjectTrack track ) => EditableTracks.FirstOrDefault( x => x.Track == track );

	private static IEnumerable<ITrackView> EnumerateDescendants( ITrackView track ) =>
		[track, ..track.Children.SelectMany( EnumerateDescendants )];

	private static IEnumerable<ITrackView> EnumerateVisibleDescendants( ITrackView track ) =>
		track.IsExpanded
			? [track, ..track.Children.SelectMany( EnumerateVisibleDescendants )]
			: [track];

	void Update();
}

/// <summary>
/// Describes how a track should be displayed in the track list / dope sheet.
/// </summary>
public interface ITrackView
{
	ITrackListView TrackList { get; }

	bool IsExpanded { get; set; }
	bool IsLockedSelf { get; set; }
	bool IsPlaybackDisabled { get; set; }

	public bool IsLocked => IsLockedSelf || Parent?.IsLocked is true;

	float Position { get; }
	float Height { get; }

	ITrackView? Parent { get; }

	public string Title => Track.Name;
	public string Description
	{
		get
		{
			var path = Track.GetPath();
			string[] propertyNames = [path.ReferenceTrack.Name, .. path.PropertyNames];
			return string.Join( $" \u2192 ", propertyNames );
		}
	}

	IProjectTrack Track { get; }
	ITrackTarget Target { get; }
	IReadOnlyList<ITrackView> Children { get; }
	IEnumerable<(IPropertyBlock Block, MovieTime? Offset)> Blocks { get; }

	int StateHash { get; }

	/// <summary>
	/// Invoked when properties of this track are changed.
	/// </summary>
	event Action<ITrackView> Changed;

	/// <summary>
	/// Invoked when the contents of the track are modified.
	/// </summary>
	event Action<ITrackView> ValueChanged;

	/// <summary>
	/// Invoked when this track is removed.
	/// </summary>
	event Action<ITrackView> Removed;

	void Remove();
	bool NoteInteraction();

	public void InspectProperty()
	{
		if ( Target is not { } property ) return;
		if ( property.GetTargetGameObject() is not { } go ) return;

		SceneEditorSession.Active.Selection.Clear();
		SceneEditorSession.Active.Selection.Add( go );

		if ( Track.Parent is not IReferenceTrack<GameObject> )
		{
			return;
		}

		switch ( property.Name )
		{
			case nameof( GameObject.LocalPosition ):
				EditorToolManager.SetSubTool( nameof( PositionEditorTool ) );
				break;

			case nameof( GameObject.LocalRotation ):
				EditorToolManager.SetSubTool( nameof( RotationEditorTool ) );
				break;

			case nameof( GameObject.LocalScale ):
				EditorToolManager.SetSubTool( nameof( ScaleEditorTool ) );
				break;
		}
	}
}

partial class Session
{
	private ITrackListView? _trackList;
	private float _trackListOffset;

	/// <summary>
	/// Which tracks should be visible in the track list / dope sheet.
	/// </summary>
	public ITrackListView TrackList => _trackList ??= new DefaultTrackListView( this );

	public float TrackListOffset
	{
		get => _trackListOffset;
		set
		{
			if ( _trackListOffset.Equals( value ) ) return;

			_trackListOffset = value;
			ViewChanged?.Invoke();
		}
	}
}

file sealed class DefaultTrackListView : ITrackListView
{
	public Session Session { get; }
	public int StateHash { get; private set; }

	private readonly SynchronizedList<IProjectTrack, DefaultTrackView> _rootTracks;

	public IReadOnlyList<ITrackView> RootTracks => _rootTracks;

	public event Action<ITrackListView>? Changed;

	public DefaultTrackListView( Session session )
	{
		Session = session;

		_rootTracks = new SynchronizedList<IProjectTrack, DefaultTrackView>(
			AddRootTrack, RemoveRootTrack, UpdateRootTrack );

		Update();
	}

	private DefaultTrackView AddRootTrack( IProjectTrack source ) =>
		new ( this, null, source, Session.Binder.Get( source ) );

	private void RemoveRootTrack( IProjectTrack source, DefaultTrackView item ) =>
		item.OnRemoved();

	private bool UpdateRootTrack( IProjectTrack source, DefaultTrackView item ) =>
		item.Update();

	public void Update()
	{
		if ( !_rootTracks.Update( Session.Project.RootTracks ) ) return;

		var position = 0f;
		var hashCode = new HashCode();

		foreach ( var track in _rootTracks )
		{
			track.UpdatePosition( ref position );
			hashCode.Add( track.StateHash );

			position += 8f;
		}

		StateHash = hashCode.ToHashCode();

		Changed?.Invoke( this );
	}
}

file sealed class DefaultTrackView
	: ITrackView, IComparable<DefaultTrackView>
{
	private DefaultTrackListView TrackList { get; }

	ITrackListView ITrackView.TrackList => TrackList;

	public float Position { get; private set; } = -1f;
	public float Height { get; private set; } = -1f;

	public ITrackView? Parent { get; }
	public IProjectTrack Track { get; }
	public ITrackTarget Target { get; }

	private bool _isExpanded;
	private bool _isLockedSelf;

	private bool _wasExpanded;

	public bool IsExpanded
	{
		get => _isExpanded;
		set
		{
			if ( _isExpanded == value ) return;

			_isExpanded = value;

			SetCookie( nameof(IsExpanded), value );
			TrackList.Update();
		}
	}

	public bool IsLockedSelf
	{
		get => _isLockedSelf;
		set
		{
			if ( _isLockedSelf == value ) return;

			_isLockedSelf = value;

			SetCookie( nameof(IsLockedSelf), value );
			DispatchChanged( true );
		}
	}

	public bool IsPlaybackDisabled { get; set; }

	private readonly SynchronizedList<IProjectTrack, DefaultTrackView> _children;

	public IReadOnlyList<ITrackView> Children => _children;

	public int StateHash { get; private set; }

	public event Action<ITrackView>? Changed;
	public event Action<ITrackView>? Removed;
	public event Action<ITrackView>? ValueChanged;

	public IEnumerable<(IPropertyBlock Block, MovieTime? Offset)> Blocks
	{
		get
		{
			if ( Track is not IProjectPropertyTrack propertyTrack ) return [];

			var editMode = TrackList.Session.EditMode;

			var previewBlocks = editMode?.GetPreviewBlocks( propertyTrack )
				.Select( x => (x, (MovieTime?)editMode.PreviewBlockOffset) );

			return propertyTrack.Blocks
				.Select( x => ((IPropertyBlock)x, (MovieTime?)null) )
				.Concat( previewBlocks ?? [] );
		}
	}

	public DefaultTrackView( DefaultTrackListView trackList, DefaultTrackView? parent, IProjectTrack track,
		ITrackTarget target )
	{
		TrackList = trackList;
		Parent = parent;
		Track = track;
		Target = target;

		_isExpanded = GetCookie( nameof(IsExpanded), true );
		_isLockedSelf = GetCookie( nameof(IsLockedSelf), false );

		_children = new SynchronizedList<IProjectTrack, DefaultTrackView>(
			AddChildTrack, RemoveChildTrack, UpdateChildTrack );
	}

	private void DispatchChanged( bool recurse )
	{
		Changed?.Invoke( this );

		if ( !recurse ) return;

		foreach ( var child in _children )
		{
			child.DispatchChanged( true );
		}
	}

	private DefaultTrackView AddChildTrack( IProjectTrack source )
	{
		return new( TrackList, this, source, TrackList.Session.Binder.Get( source ) );
	}

	private void RemoveChildTrack( IProjectTrack source, DefaultTrackView item ) => item.OnRemoved();
	private bool UpdateChildTrack( IProjectTrack source, DefaultTrackView item ) => item.Update();

	public bool Update() => _children.Update( Track.Children ) || _wasExpanded != IsExpanded;

	public bool UpdatePosition( ref float position )
	{
		var changed = !Position.Equals( position ) || _wasExpanded != IsExpanded;
		var hashCode = new HashCode();

		hashCode.Add( Track );
		hashCode.Add( IsExpanded );

		Position = position;
		_wasExpanded = IsExpanded;

		position += DopeSheet.TrackHeight;

		var childPosition = position;

		foreach ( var child in _children )
		{
			changed |= child.UpdatePosition( ref childPosition );
			hashCode.Add( child.StateHash );
		}

		if ( IsExpanded )
		{
			position = childPosition;
		}

		Height = position - Position;
		StateHash = hashCode.ToHashCode();

		if ( changed ) Changed?.Invoke( this );

		return changed;
	}

	private bool _removed;

	internal void OnRemoved()
	{
		if ( _removed ) return;
		_removed = true;

		_children.Clear();

		Removed?.Invoke( this );
	}

	public void Remove()
	{
		Track.Remove();
		TrackList.Update();
	}

	public bool NoteInteraction()
	{
		ValueChanged?.Invoke( this );
		return true;
	}

	public int CompareTo( DefaultTrackView? other )
	{
		if ( ReferenceEquals( this, other ) )
		{
			return 0;
		}

		if ( other is null )
		{
			return 1;
		}

		var childrenCompare = (Children.Count > 0).CompareTo( other.Children.Count > 0 );
		if ( childrenCompare != 0 ) return childrenCompare;

		return string.Compare( Track.Name, other.Track.Name, StringComparison.Ordinal );
	}

	private T GetCookie<T>( string name, T fallback ) =>
		TrackList.Session.GetCookie( $"{Track.Id}.{name}", fallback );

	private void SetCookie<T>( string name, T value ) =>
		TrackList.Session.SetCookie( $"{Track.Id}.{name}", value );
}
