using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Describes which tracks should be shown in the track list / dope sheet.
/// </summary>
public partial interface ITrackListView
{
	IReadOnlyList<ITrackView> RootTracks { get; }
	int StateHash { get; }
	MovieTime PreviewOffset { get; }

	/// <summary>
	/// Invoked when tracks are added or removed.
	/// </summary>
	event Action<ITrackListView> Changed;

	public IEnumerable<ITrackView> AllTracks => RootTracks.SelectMany( EnumerateDescendants );
	public IEnumerable<ITrackView> VisibleTracks => RootTracks.SelectMany( EnumerateVisibleDescendants );

	public IEnumerable<ITrackView> EditableTracks =>
		AllTracks.Where( x => x is { IsLocked: false, Target: ITrackProperty { CanWrite: true } } );

	public ITrackView? Find( IProjectTrack track ) => AllTracks.FirstOrDefault( x => x.Track == track );

	public ITrackView? Find( GameObject go ) => AllTracks.FirstOrDefault( x =>
		x.Target is ITrackReference<GameObject> { IsBound: true } target && target.Value == go );
	public ITrackView? Find( Component cmp ) => AllTracks.FirstOrDefault( x =>
		x.Target is ITrackReference { IsBound: true } target && target.Value == cmp );
	public ITrackView? Find( MovieResource resource ) => AllTracks.FirstOrDefault( x =>
		x.Track is ProjectSequenceTrack );

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
	IEnumerable<ITrackBlock> Blocks { get; }
	IEnumerable<ITrackBlock> PreviewBlocks { get; }

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
	bool MarkValueChanged();

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

	public ITrackView? Find( string propertyPath )
	{
		var parent = this;

		while ( parent is not null && propertyPath.Length > 0 )
		{
			var propertyName = propertyPath;

			// TODO: Hack for anim graph parameters including periods

			if ( parent.Track.TargetType != typeof( SkinnedModelRenderer.ParameterAccessor ) && propertyPath.IndexOf( '.' ) is var index and > -1 )
			{
				propertyName = propertyPath[..index];
				propertyPath = propertyPath[(index + 1)..];
			}
			else
			{
				propertyPath = string.Empty;
			}

			parent = parent.Children.FirstOrDefault( x => x.Track.Name == propertyName );
		}

		return parent;
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

	public float TrackListScrollOffset
	{
		get => _trackListOffset;
		set
		{
			if ( _trackListOffset.Equals( value ) ) return;

			_trackListOffset = value;
			ViewChanged?.Invoke();
		}
	}

	private void TrackFrame()
	{
		((DefaultTrackListView?)_trackList)?.Frame();
	}
}

file sealed class DefaultTrackListView : ITrackListView
{
	public Session Session { get; }
	public int StateHash { get; private set; }

	public MovieTime PreviewOffset => Session.EditMode?.PreviewBlockOffset ?? default;

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
		if ( !_rootTracks.Update( Session.Project.RootTracks.Order() ) ) return;

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

	public void Frame()
	{
		foreach ( var track in _rootTracks )
		{
			track.Frame();
		}
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

	private readonly SynchronizedList<IProjectTrack, DefaultTrackView> _children;

	private readonly List<ITrackBlock> _blocks = new();
	private readonly List<ITrackBlock> _previewBlocks = new();

	private bool _blocksInvalid = true;
	private bool _previewBlocksInvalid = true;
	private bool _dispatchValueChanged = false;

	public IReadOnlyList<ITrackView> Children => _children;

	public int StateHash { get; private set; }

	public event Action<ITrackView>? Changed;
	public event Action<ITrackView>? Removed;
	public event Action<ITrackView>? ValueChanged;

	public IEnumerable<ITrackBlock> Blocks
	{
		get
		{
			UpdateBlocks();
			return _blocks;
		}
	}

	public IEnumerable<ITrackBlock> PreviewBlocks
	{
		get
		{
			UpdatePreviewBlocks();
			return _previewBlocks;
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

	private void UpdateBlocks()
	{
		if ( !_blocksInvalid ) return;

		_blocksInvalid = false;
		_blocks.Clear();

		foreach ( var child in Children )
		{
			AddChildBlocks( _blocks, child.Blocks );
		}

		if ( Track is IProjectPropertyTrack propertyTrack )
		{
			_blocks.AddRange( propertyTrack.Blocks );
		}

		if ( Track is ProjectSequenceTrack sequenceTrack )
		{
			_blocks.AddRange( sequenceTrack.Blocks );
		}
	}

	private void UpdatePreviewBlocks()
	{
		if ( !_previewBlocksInvalid ) return;

		_previewBlocksInvalid = false;
		_previewBlocks.Clear();

		foreach ( var child in Children )
		{
			AddChildBlocks( _previewBlocks, child.PreviewBlocks );
		}

		if ( Track is IProjectPropertyTrack propertyTrack && TrackList.Session.EditMode is { } editMode )
		{
			_previewBlocks.AddRange( editMode.GetPreviewBlocks( propertyTrack ) );
		}
	}

	private static PropertySignal<object?> DefaultSignal { get; } = (object?)null;

	/// <summary>
	/// Merge the current <paramref name="list"/> with blocks from <paramref name="blocks"/>, assuming
	/// both are already sorted.
	/// </summary>
	private void AddChildBlocks( List<ITrackBlock> list, IEnumerable<ITrackBlock> blocks )
	{
		if ( list.Count == 0 )
		{
			foreach ( var block in blocks )
			{
				list.Add( new PropertyBlock<object?>( DefaultSignal, block.TimeRange ) );
			}

			return;
		}

		var union = list
			.Select( x => x.TimeRange )
			.Union( blocks.Select( x => x.TimeRange ) )
			.ToArray();

		list.Clear();
		list.AddRange( union.Select( x => new PropertyBlock<object?>( DefaultSignal, x ) ) );
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

	public bool MarkValueChanged()
	{
		_blocksInvalid = true;
		_previewBlocksInvalid = true;
		_dispatchValueChanged = true;

		Parent?.MarkValueChanged();
		TrackList.Session.ClipModified();

		return true;
	}

	public void Frame()
	{
		if ( _dispatchValueChanged )
		{
			_dispatchValueChanged = false;
			ValueChanged?.Invoke( this );
		}

		foreach ( var child in _children )
		{
			child.Frame();
		}
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
