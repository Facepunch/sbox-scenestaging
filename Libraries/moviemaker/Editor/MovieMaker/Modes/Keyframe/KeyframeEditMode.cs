using Sandbox.MovieMaker;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

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
	private MovieTime _lastDragTime;

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

	private TimelineTrack? GetTimelineTrack( TrackView view )
	{
		if ( view.Track is not IProjectPropertyTrack ) return null;
		if ( view.Target is not ITrackProperty { IsBound: true, CanWrite: true } ) return null;

		return Timeline.Tracks.FirstOrDefault( x => x.View == view );
	}

	private List<KeyframeHandle>? GetHandles( TrackView view )
	{
		return GetTimelineTrack( view ) is { } timelineTrack ? GetHandles( timelineTrack ) : null;
	}

	private List<KeyframeHandle>? GetHandles( TimelineTrack timelineTrack )
	{
		// Handle list should already exist from OnUpdateTimelineItems

		return _keyframeHandles.GetValueOrDefault( timelineTrack );
	}

	/// <summary>
	/// Creates or updates the <see cref="KeyframeHandle"/> for a given <paramref name="keyframe"/>.
	/// Will update a keyframe that already exists if it has the exact same <see cref="Keyframe.Time"/>.
	/// </summary>
	private bool CreateOrUpdateKeyframeHandle( TrackView view, Keyframe keyframe )
	{
		if ( GetTimelineTrack( view ) is not { } timelineTrack ) return false;
		if ( GetHandles( timelineTrack ) is not { } handles ) return false;

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
		if ( timelineTrack.View.Track is not IProjectPropertyTrack track ) return;

		handles.Sort();

		var block = new List<Keyframe>();
		var blocks = new List<IProjectPropertyBlock>();

		foreach ( var handle in handles )
		{
			block.Add( handle.Keyframe );

			if ( handle.EndBlock || handle == handles[^1] )
			{
				blocks.Add( FinishBlock( timelineTrack.View.Track.TargetType, block ) );
				block.Clear();
			}
		}

		track.SetBlocks( blocks );

		timelineTrack.View.MarkValueChanged();
	}

	private IProjectPropertyBlock FinishBlock( Type propertyType, IReadOnlyList<Keyframe> keyframes )
	{
		var start = keyframes[0].Time;
		var end = keyframes[^1].Time;

		var signal = PropertySignal.FromKeyframes( propertyType, keyframes );

		return PropertyBlock.FromSignal( signal, (start, end) );
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

		_lastDragTime = handle.Time;
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
		var transform = new MovieTransform( time - _lastDragTime );

		_lastDragTime = time;

		Session.SetCurrentPointer( time );

		foreach ( var keyframe in SelectedKeyframes )
		{
			keyframe.Time = transform * keyframe.Time;
		}

		UpdateTracksFromHandles( SelectedKeyframes );
	}

	internal void KeyframeDragEnd( KeyframeHandle handle, GraphicsMouseEvent e )
	{
		if ( !_isDraggingKeyframes ) return;

		e.Accepted = true;

		_isDraggingKeyframes = false;

		ClearKeyframeChangeScope();
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
		var selected = SelectedKeyframes
			.ToImmutableHashSet();

		var tracks = SelectedKeyframes
			.Select( x => x.Parent )
			.Distinct()
			.ToArray();

		foreach ( var timelineTrack in tracks )
		{
			if ( !_keyframeHandles.TryGetValue( timelineTrack, out var handles ) ) continue;

			handles.RemoveAll( selected.Contains );

			UpdateTrackFromHandles( timelineTrack );
		}

		foreach ( var keyframe in SelectedKeyframes )
		{
			keyframe.Destroy();
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
