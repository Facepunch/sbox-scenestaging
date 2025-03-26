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

	/// <summary>
	/// Invoked when tracks are added or removed.
	/// </summary>
	event Action<ITrackListView> Changed;

	public IEnumerable<ITrackView> AllTracks => RootTracks.SelectMany( EnumerateDescendants );

	public IEnumerable<ITrackView> EditableTracks =>
		AllTracks.Where( x => x is { IsLocked: false, Target: ITrackProperty { CanWrite: true } } );

	public ITrackView? Find( IProjectTrack track ) => AllTracks.FirstOrDefault( x => x.Track == track );
	public ITrackView? FindEditable( IProjectTrack track ) => EditableTracks.FirstOrDefault( x => x.Track == track );

	private static IEnumerable<ITrackView> EnumerateDescendants( ITrackView track ) =>
		[track, ..track.VisibleChildren.SelectMany( EnumerateDescendants )];

	void Update();
}

/// <summary>
/// Describes how a track should be displayed in the track list / dope sheet.
/// </summary>
public interface ITrackView
{
	ITrackListView TrackList { get; }

	bool IsLocked { get; }
	bool IsExpanded { get; set; }
	bool HasChildren { get; }

	bool IsLockedSelf { get; set; }

	float Position { get; }

	ITrackView? Parent { get; }

	IProjectTrack Track { get; }
	ITrackTarget Target { get; }
	IReadOnlyList<ITrackView> VisibleChildren { get; }

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

	/// <summary>
	/// Which tracks should be visible in the track list / dope sheet.
	/// </summary>
	public ITrackListView TrackList => _trackList ??= new DefaultTrackListView( this );
}

file sealed class DefaultTrackListView : ITrackListView
{
	public Session Session { get; }

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

		foreach ( var track in _rootTracks )
		{
			track.UpdatePosition( ref position );
		}

		Changed?.Invoke( this );
	}
}

file sealed class DefaultTrackView
	: ITrackView, IComparable<DefaultTrackView>
{
	private DefaultTrackListView TrackList { get; }

	ITrackListView ITrackView.TrackList => TrackList;

	public float Position { get; private set; } = -1f;

	public ITrackView? Parent { get; }
	public IProjectTrack Track { get; }
	public ITrackTarget Target { get; }

	public bool IsLocked => IsLockedSelf || Parent?.IsLocked is true;
	public bool HasChildren => Track.Children.Count > 0;

	public bool IsExpanded { get; set; } = true;
	public bool IsLockedSelf { get; set; }

	private readonly SynchronizedList<IProjectTrack, DefaultTrackView> _children;

	public IReadOnlyList<ITrackView> VisibleChildren => _children;

	public event Action<ITrackView>? Changed;
	public event Action<ITrackView>? Removed;
	public event Action<ITrackView>? ValueChanged;

	public DefaultTrackView( DefaultTrackListView trackList, DefaultTrackView? parent, IProjectTrack track,
		ITrackTarget target )
	{
		TrackList = trackList;
		Parent = parent;
		Track = track;
		Target = target;

		_children = new SynchronizedList<IProjectTrack, DefaultTrackView>(
			AddChildTrack, RemoveChildTrack, UpdateChildTrack );
	}

	private DefaultTrackView AddChildTrack( IProjectTrack source ) =>
		new( TrackList, this, source, TrackList.Session.Binder.Get( source ) );

	private void RemoveChildTrack( IProjectTrack source, DefaultTrackView item ) => item.OnRemoved();
	private bool UpdateChildTrack( IProjectTrack source, DefaultTrackView item ) => item.Update();

	public bool Update() => _children.Update( IsExpanded ? Track.Children : [] );

	public bool UpdatePosition( ref float position )
	{
		var changed = !Position.Equals( position );

		Position = position;

		position += DopeSheet.TrackHeight;

		foreach ( var child in _children )
		{
			changed |= child.UpdatePosition( ref position );
		}

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

		var childrenCompare = HasChildren.CompareTo( other.HasChildren );
		if ( childrenCompare != 0 ) return childrenCompare;

		return string.Compare( Track.Name, other.Track.Name, StringComparison.Ordinal );
	}
}
