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

	private readonly List<DefaultTrackView> _rootTracks = new();

	public IReadOnlyList<ITrackView> RootTracks => _rootTracks;

	public event Action<ITrackListView>? Changed;

	public DefaultTrackListView( Session session )
	{
		Session = session;
		Update();
	}

	public bool Update()
	{
		_rootTracks.RemoveAll( x => !Session.Project.RootTracks.Contains( x.Track ) );

		foreach ( var track in Session.Project.RootTracks )
		{
			if ( _rootTracks.Any( x => x.Track == track ) ) continue;

			_rootTracks.Add( new DefaultTrackView( this, null, track, Session.Binder.Get( track ) ) );
		}

		_rootTracks.Sort();

		var changed = false;
		var position = 0f;

		foreach ( var track in _rootTracks )
		{
			changed |= track.Update( ref position );
		}

		if ( changed )
		{
			Changed?.Invoke( this );
		}

		return changed;
	}
}

file sealed class DefaultTrackView
	: ITrackView, IComparable<DefaultTrackView>
{
	private DefaultTrackListView TrackList { get; }

	ITrackListView ITrackView.TrackList => TrackList;

	public float Position { get; private set; }

	public ITrackView? Parent { get; }
	public IProjectTrack Track { get; }
	public ITrackTarget Target { get; }

	public bool IsLocked => IsLockedSelf || Parent?.IsLocked is true;
	public bool HasChildren => Track.Children.Count > 0;

	public bool IsExpanded { get; set; } = true;
	public bool IsLockedSelf { get; set; }

	private readonly List<DefaultTrackView> _children = new();

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
	}

	public bool Update( ref float position )
	{
		var changed = !Position.Equals( position );

		Position = position;

		position += DopeSheet.TrackHeight;

		var toRemove = IsExpanded
			? _children.Where( x => !Track.Children.Contains( x.Track ) ).ToArray()
			: _children.ToArray();

		foreach ( var track in toRemove )
		{
			_children.Remove( track );
			track.OnRemoved();

			changed = true;
		}

		if ( IsExpanded )
		{
			foreach ( var track in Track.Children )
			{
				if ( _children.Any( x => x.Track == track ) ) continue;

				_children.Add( new DefaultTrackView( TrackList, this, track, TrackList.Session.Binder.Get( track ) ) );

				changed = true;
			}

			_children.Sort();

			foreach ( var child in _children )
			{
				changed |= child.Update( ref position );
			}
		}

		if ( changed )
		{
			Changed?.Invoke( this );
		}

		return changed;
	}

	private void OnRemoved()
	{
		foreach ( var child in _children )
		{
			child.OnRemoved();
		}

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
