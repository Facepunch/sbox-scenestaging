using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[Title( "Keyframe Editor" ), Icon( "key" ), Order( 0 )]
[Description( "Add or modify keyframes on tracks." )]
public sealed partial class KeyframeEditMode : EditMode
{
	public bool AutoCreateTracks { get; set; }
	public bool CreateKeyframeOnClick { get; set; }

	public KeyframeInterpolation DefaultInterpolation { get; set; } = KeyframeInterpolation.Cubic;

	public IEnumerable<KeyframeHandle> SelectedKeyframes => Timeline.SelectedItems.OfType<KeyframeHandle>();

	protected override void OnEnable()
	{
		AddClipboardToolbarGroup();

		var changesGroup = ToolBar.AddGroup();

		changesGroup.AddToggle( new( "Automatic Track Creation", "playlist_add",
				"When enabled, tracks will be automatically created when making changes in the scene." ),
			() => AutoCreateTracks,
			value => AutoCreateTracks = value );

		changesGroup.AddToggle( new( "Create Keyframe on Click", "edit",
			"When enabled, clicking on a track in the timeline will create a keyframe." ),
			() => CreateKeyframeOnClick,
			value => CreateKeyframeOnClick = value );

		var selectionGroup = ToolBar.AddGroup();

		selectionGroup.AddInterpolationSelector( () =>
		{
			KeyframeInterpolation? interpolation = null;

			foreach ( var handle in SelectedKeyframes )
			{
				interpolation ??= handle.Keyframe.Interpolation;

				if ( interpolation != handle.Keyframe.Interpolation ) return KeyframeInterpolation.Unknown;
			}

			return interpolation ?? DefaultInterpolation;
		}, value =>
		{
			DefaultInterpolation = value;

			foreach ( var handle in SelectedKeyframes )
			{
				handle.Keyframe = handle.Keyframe with { Interpolation = value };
			}

			UpdateTracksFromHandles( SelectedKeyframes );
		} );
	}

	public override bool AllowTrackCreation => AutoCreateTracks;

	private bool _isDraggingKeyframes;

	private MovieTime _dragStartTime;
	private readonly Dictionary<TrackView, KeyframeModification> _modifications = new();

	private sealed record KeyframeChangeScope( string Name, TrackView? TrackView, IHistoryScope HistoryScope ) : IDisposable
	{
		public void Dispose() => HistoryScope.Dispose();
	}

	private KeyframeChangeScope? _changeScope;

	private IHistoryScope GetKeyframeChangeScope( string name, TrackView? trackView = null )
	{
		if ( _changeScope is { } scope && scope.TrackView == trackView && scope.Name == name ) return _changeScope.HistoryScope;

		_changeScope = new KeyframeChangeScope( name, trackView,
			Session.History.Push( trackView is null ? $"{name} Keyframes" : $"{name} Keyframes ({trackView.Track.Name})" ) );

		return _changeScope.HistoryScope;
	}

	private void ClearKeyframeChangeScope()
	{
		_changeScope = null;
	}

	protected override bool OnPreChange( TrackView view )
	{
		// Touching a property should create a keyframe

		return CreateOrUpdateKeyframeHandle( view, new Keyframe( Session.CurrentPointer, view.Target.Value, DefaultInterpolation ) );
	}

	protected override bool OnPostChange( TrackView view )
	{
		// We've finished changing a property, update the keyframe we created in OnPreChange

		return CreateOrUpdateKeyframeHandle( view, new Keyframe( Session.CurrentPointer, view.Target.Value, DefaultInterpolation ) );
	}

	private readonly Dictionary<TimelineTrack, List<KeyframeHandle>> _keyframeHandles = new();

	/// <summary>
	/// Creates or updates the <see cref="KeyframeHandle"/> for a given <paramref name="keyframe"/>.
	/// </summary>
	private bool CreateOrUpdateKeyframeHandle( TrackView view, Keyframe keyframe )
	{
		if ( view.Track is not IProjectPropertyTrack propertyTrack ) return false;
		if ( view.Target is not ITrackProperty { IsBound: true, CanWrite: true } ) return false;

		using var scope = GetKeyframeChangeScope( "Set", view );

		if ( Timeline.Tracks.FirstOrDefault( x => x.View == view ) is not { } timelineTrack ) return false;

		// Handle list should already exist from OnUpdateTimelineItems

		if ( !_keyframeHandles.TryGetValue( timelineTrack, out var handles ) ) return false;

		if ( handles.FirstOrDefault( x => x.Time == keyframe.Time ) is { } handle )
		{
			if ( handle.Keyframe.Equals( keyframe ) ) return false;

			handle.Keyframe = keyframe;
		}
		else
		{
			handles.Add( new KeyframeHandle( timelineTrack, keyframe ) );
			handles.Sort();
		}

		return true;
	}

	protected override void OnPreRestore()
	{
		foreach ( var timelineTrack in Timeline.Tracks )
		{
			ClearTimelineItems( timelineTrack );
		}
	}

	protected override void OnUpdateTimelineItems( TimelineTrack timelineTrack )
	{
		if ( _keyframeHandles.ContainsKey( timelineTrack ) )
		{
			return;
		}

		// Only update handles if they don't exist yet, because handles are authoritative

		_keyframeHandles.Add( timelineTrack, new List<KeyframeHandle>() );

		UpdateKeyframeHandles( timelineTrack );
	}

	private void UpdateKeyframeHandles( TimelineTrack timelineTrack )
	{
		if ( !_keyframeHandles.TryGetValue( timelineTrack, out var handles ) ) return;

		foreach ( var handle in handles )
		{
			handle.Destroy();
		}

		handles.Clear();

		foreach ( var keyframe in timelineTrack.View.Keyframes )
		{
			handles.Add( new KeyframeHandle( timelineTrack, keyframe ) );
		}
	}

	private void UpdateTracksFromHandles( IEnumerable<KeyframeHandle> handles )
	{
		var tracks = handles
			.Select( x => x.Parent )
			.Distinct();

		foreach ( var timelineTrack in tracks )
		{
			UpdateTrackFromHandles( timelineTrack );
		}
	}

	private void UpdateTrackFromHandles( TimelineTrack timelineTrack )
	{
		if ( !_keyframeHandles.TryGetValue( timelineTrack, out var handles ) ) return;

		// TODO
	}

	protected override void OnClearTimelineItems( TimelineTrack timelineTrack )
	{
		if ( !_keyframeHandles.Remove( timelineTrack, out var handles ) ) return;

		foreach ( var handle in handles )
		{
			handle.Destroy();
		}
	}

	private IEnumerable<IGrouping<TrackView, KeyframeHandle>> GetSelectedKeyframes( KeyframeHandle? include = null )
	{
		var handles = SelectedKeyframes;

		if ( include is not null )
		{
			handles = handles.Union( [include] );
		}

		return handles.GroupBy( x => x.Parent.View );
	}

	private static IReadOnlySet<MovieTime> GetKeyframeTimes( IEnumerable<KeyframeHandle> handles ) =>
		handles.Select( x => x.Time ).ToImmutableHashSet();

	private static IReadOnlyList<IProjectPropertyBlock> GetAffectedBlocks( TrackView view, IReadOnlySet<MovieTime> keyframeTimes ) =>
	[
		..view.Blocks
			.OfType<IProjectPropertyBlock>()
			.Where( x => x.Signal.HasKeyframes )
			.Where( x => x.Signal.GetKeyframes( x.TimeRange )
				.Select( y => y.Time )
				.Intersect( keyframeTimes )
				.Any() )
	];

	protected override void OnKeyPress( KeyEvent e )
	{
		base.OnKeyPress( e );

		if ( e.Key == KeyCode.Shift )
		{
			CreateKeyframeOnClick = true;
		}
	}

	protected override void OnKeyRelease( KeyEvent e )
	{
		base.OnKeyRelease( e );

		if ( e.Key == KeyCode.Shift )
		{
			CreateKeyframeOnClick = false;
		}

		if ( e.Key == KeyCode.Escape )
		{
			AutoCreateTracks = false;
			CreateKeyframeOnClick = false;
		}
	}

	protected override void OnMouseRelease( MouseEvent e )
	{
		if ( !e.LeftMouseButton || !CreateKeyframeOnClick ) return;

		var scenePos = Timeline.ToScene( e.LocalPosition );

		if ( Timeline.Tracks.FirstOrDefault( x => x.SceneRect.IsInside( scenePos ) ) is not { } timelineTrack ) return;

		var view = timelineTrack.View;
		var time = Session.ScenePositionToTime( scenePos );

		if ( view.Track is not IProjectPropertyTrack propertyTrack ) return;
		if ( view.Target is not ITrackProperty { IsBound: true, CanWrite: true } target ) return;

		if ( !_keyframeHandles.TryGetValue( timelineTrack, out var handles ) ) return;
		if ( handles.Any( x => x.Time == time ) ) return;

		var value = propertyTrack.TryGetValue( time, out var val ) ? val : target.Value;

		ClearKeyframeChangeScope();

		using var scope = Session.History.Push( "Add Keyframe" );

		handles.Add( new KeyframeHandle( timelineTrack, new Keyframe( time, value, DefaultInterpolation ) ) );

		UpdateTrackFromHandles( timelineTrack );
	}

	internal void KeyframeDragStart( KeyframeHandle handle, GraphicsMouseEvent e )
	{
		DefaultInterpolation = handle.Keyframe.Interpolation;

		Session.SetCurrentPointer( handle.Time );

		handle.View.InspectProperty();

		_dragStartTime = handle.Time;

		_modifications.Clear();

		foreach ( var group in GetSelectedKeyframes( handle ) )
		{
			_modifications.Add( group.Key, new KeyframeModification( group.Key, group ) );
		}
	}

	internal void KeyframeDragMove( KeyframeHandle handle, GraphicsMouseEvent e )
	{
		e.Accepted = true;

		_isDraggingKeyframes = true;

		var view = SelectedKeyframes.GroupBy( x => x.View )
			.Count() == 1
			? handle.View
			: null;

		using var scope = GetKeyframeChangeScope( "Move", view );

		var time = Session.ScenePositionToTime( e.ScenePosition, new SnapOptions( SnapFlag.PlayHead ) );
		var transform = new MovieTransform( time - _dragStartTime );

		foreach ( var modification in _modifications.Values )
		{
			modification.Update( transform );
		}

		Session.SetCurrentPointer( time );
	}

	internal void KeyframeDragEnd( KeyframeHandle handle, GraphicsMouseEvent e )
	{
		if ( !_isDraggingKeyframes ) return;

		e.Accepted = true;

		_isDraggingKeyframes = false;

		var view = SelectedKeyframes.GroupBy( x => x.View )
			.Count() == 1
			? handle.View
			: null;

		using var scope = GetKeyframeChangeScope( "Move", view );

		foreach ( var modification in _modifications.Values )
		{
			modification.Commit();
		}

		ClearKeyframeChangeScope();

		_modifications.Clear();
	}

	protected override void OnSelectAll()
	{
		foreach ( var handle in _keyframeHandles.SelectMany( x => x.Value ) )
		{
			handle.Selected = true;
		}
	}

	protected override void OnDelete()
	{
		ApplyChangeToSelection( new KeyframeDeletion( [] ) );
	}

	public bool ApplyChangeToSelection( KeyframeChanges change )
	{
		var anyChanges = false;

		ClearKeyframeChangeScope();

		using var scope = Session.History.Push( $"{change.Name} Keyframes" );

		foreach ( var group in GetSelectedKeyframes() )
		{
			var keyframeTimes = GetKeyframeTimes( group );
			var blocks = GetAffectedBlocks( group.Key, keyframeTimes );

			var trackChange = change with { KeyframeTimes = [..keyframeTimes] };

			anyChanges |= ApplyChange( group.Key, blocks, trackChange );
		}

		return anyChanges;
	}

	public static bool ApplyChange( TrackView view, IReadOnlyList<IProjectPropertyBlock> blocks, KeyframeChanges changes )
	{
		if ( view.Track is not IProjectPropertyTrack propertyTrack || blocks.Count <= 0 ) return false;

		foreach ( var block in blocks )
		{
			propertyTrack.Remove( block.TimeRange );
		}

		foreach ( var block in blocks )
		{
			if ( block.WithKeyframeChanges( changes ) is { } changed )
			{
				propertyTrack.Add( changed );
			}
		}

		view.MarkValueChanged();

		return true;
	}

	private sealed class KeyframeModification
	{
		private readonly ImmutableHashSet<MovieTime> _times;

		private readonly ImmutableArray<KeyframeHandle> _handles;
		private readonly ImmutableArray<IProjectPropertyBlock> _blocks;

		private KeyframeChanges? _changes;

		public TrackView View { get; }

		public KeyframeModification( TrackView view, IEnumerable<KeyframeHandle> handles )
		{
			View = view;

			_handles = [..handles.OrderBy( x => x.Time )];

			foreach ( var handle in _handles )
			{
				handle.OriginalKeyframe = handle.Keyframe;
			}

			_times = GetKeyframeTimes( _handles ).ToImmutableHashSet();
			_blocks = [..GetAffectedBlocks( view, _times )];
		}

		public void Update( MovieTransform transform ) =>
			Update( transform == MovieTransform.Identity ? null : new KeyframeTransform( _times, transform ) );

		public void Update( KeyframeChanges? changes )
		{
			_changes = changes;

			foreach ( var handle in _handles )
			{
				handle.Time = _changes?.Apply( handle.OriginalKeyframe.Time ) ?? handle.OriginalKeyframe.Time;
			}

			View.SetPreviewBlocks( _blocks, changes is not null
				? _blocks.Select( x => x.WithKeyframeChanges( changes ) ).OfType<ITrackBlock>()
				: _blocks );
		}

		public bool Commit()
		{
			if ( _changes is not { } changes ) return false;

			View.ClearPreviewBlocks();

			return ApplyChange( View, _blocks, changes );
		}

		public void Clear()
		{
			View.ClearPreviewBlocks();
		}
	}

	protected override void OnDrawGizmos( TrackView trackView, MovieTimeRange timeRange )
	{
		base.OnDrawGizmos( trackView, timeRange );

		var clampedTimeRange = timeRange.Clamp( (0d, Project.Duration) );

		foreach ( var keyframe in trackView.Keyframes )
		{
			if ( keyframe.Time < clampedTimeRange.Start ) continue;
			if ( keyframe.Time > clampedTimeRange.End ) break;

			if ( keyframe.Time == Session.CurrentPointer ) continue;

			if ( !trackView.TransformTrack.TryGetValue( keyframe.Time, out var transform ) ) continue;

			var dist = Gizmo.CameraTransform.Position.Distance( transform.Position );
			var scale = Session.GetGizmoAlpha( keyframe.Time, timeRange ) * dist / 256f;

			using var scope = Gizmo.Scope( keyframe.Time.ToString(), transform );

			var radius = scale * (Gizmo.IsHovered ? 3f : 2f);

			Gizmo.Hitbox.Sphere( new Sphere( Vector3.Zero, radius ) );
			Gizmo.Draw.Color = Color.White.Darken( Gizmo.IsHovered ? 0f : 0.125f );
			Gizmo.Draw.SolidSphere( Vector3.Zero, radius );

			if ( Gizmo.HasClicked && Gizmo.Pressed.This )
			{
				Session.SetCurrentPointer( keyframe.Time );
			}
		}
	}
}
