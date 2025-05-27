using Sandbox;
using Sandbox.MovieMaker;
using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks.Dataflow;

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

	private readonly Dictionary<TimelineTrack, TrackKeyframeHandles> _trackKeyframeHandles = new();

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

		var deleteGroup = ToolBar.AddGroup();

		deleteGroup.AddAction( new ToolBarItemDisplay( "Delete Selection", "delete",
				"Delete all selected keyframes." ),
			Delete, () => SelectedKeyframes.Any() );

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

	private TimelineTrack? GetTimelineTrack( TrackView view )
	{
		if ( view.Track is not IProjectPropertyTrack ) return null;
		if ( view.Target is not ITrackProperty { IsBound: true, CanWrite: true } ) return null;

		return Timeline.Tracks.FirstOrDefault( x => x.View == view );
	}

	private TrackKeyframeHandles? GetHandles( TimelineTrack timelineTrack )
	{
		// Handle list should already exist from OnUpdateTimelineItems

		return _trackKeyframeHandles.GetValueOrDefault( timelineTrack );
	}

	/// <summary>
	/// Creates or updates the <see cref="KeyframeHandle"/> for a given <paramref name="keyframe"/>.
	/// Will update a keyframe that already exists if it has the exact same <see cref="Keyframe.Time"/>.
	/// </summary>
	private bool CreateOrUpdateKeyframeHandle( TrackView view, Keyframe keyframe )
	{
		if ( GetTimelineTrack( view ) is not { } timelineTrack ) return false;
		if ( GetHandles( timelineTrack ) is not { } handles ) return false;

		handles.AddOrUpdate( keyframe );

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
		if ( _trackKeyframeHandles.TryGetValue( timelineTrack, out var handles ) )
		{
			handles.UpdatePositions();
			return;
		}

		// Only create / remove / modify handles if they don't exist yet, because handles are authoritative

		if ( timelineTrack.View.Track is not IProjectPropertyTrack ) return;

		handles = new TrackKeyframeHandles( timelineTrack );

		_trackKeyframeHandles.Add( timelineTrack, handles );

		handles.ReadFromTrack();
	}

	private void UpdateTracksFromHandles( IEnumerable<KeyframeHandle> handles )
	{
		var tracks = handles
			.Select( x => x.Parent )
			.Distinct();

		foreach ( var timelineTrack in tracks )
		{
			GetHandles( timelineTrack )?.WriteToTrack();
		}
	}

	protected override void OnClearTimelineItems( TimelineTrack timelineTrack )
	{
		if ( !_trackKeyframeHandles.Remove( timelineTrack, out var handles ) ) return;

		foreach ( var handle in handles )
		{
			handle.Destroy();
		}
	}

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

		if ( GetHandles( timelineTrack ) is not { } handles ) return;
		if ( handles.Any( x => x.Time == time ) ) return;

		var value = propertyTrack.TryGetValue( time, out var val ) ? val : target.Value;

		ClearKeyframeChangeScope();

		using var scope = Session.History.Push( "Add Keyframe" );

		handles.AddOrUpdate( new Keyframe( time, value, DefaultInterpolation ) );
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

		var minDelta = SelectedKeyframes
			.Select( x => -x.Time )
			.DefaultIfEmpty( 0d )
			.Max();

		var time = Session.ScenePositionToTime( e.ScenePosition, new SnapOptions( SnapFlag.PlayHead ) );

		time = MovieTime.Max( _lastDragTime + minDelta, time );

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
		foreach ( var handle in _trackKeyframeHandles.SelectMany( x => x.Value ) )
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
			if ( GetHandles( timelineTrack ) is not { } handles ) continue;

			handles.RemoveAll( selected.Contains );
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

	private sealed class TrackKeyframeHandles : IEnumerable<KeyframeHandle>
	{
		private readonly TimelineTrack _timelineTrack;
		private readonly List<KeyframeHandle> _handles = new();

		private readonly List<IProjectPropertyBlock> _sourceBlocks = new();
		private readonly List<MovieTime> _cutTimes = new();

		public TrackView View => _timelineTrack.View;
		public IProjectPropertyTrack Track => (IProjectPropertyTrack)View.Track;

		public TrackKeyframeHandles( TimelineTrack timelineTrack )
		{
			_timelineTrack = timelineTrack;
		}

		public void AddRange( IEnumerable<IKeyframe> keyframes, MovieTime timeOffset )
		{
			foreach ( var keyframe in keyframes )
			{
				var kf = new Keyframe( keyframe.Time + timeOffset, keyframe.Value, keyframe.Interpolation );

				var handle = new KeyframeHandle( _timelineTrack, kf );

				_handles.Add( handle );

				handle.Selected = true;
			}

			_handles.Sort();

			WriteToTrack();
		}

		public bool AddOrUpdate( Keyframe keyframe )
		{
			if ( _sourceBlocks.FirstOrDefault( x => x.TimeRange.Contains( keyframe.Time ) ) is { } sourceBlock )
			{
				// If keyframe is inside a source block, make its value relative

				if ( Transformer.GetDefault( Track.TargetType ) is { } transformer )
				{
					keyframe = keyframe with
					{
						Value = transformer.Difference( sourceBlock.GetValue( keyframe.Time ), keyframe.Value )
					};
				}
			}

			if ( _handles.FirstOrDefault( x => x.Time == keyframe.Time ) is { } handle )
			{
				if ( handle.Keyframe.Equals( keyframe ) ) return false;

				handle.Keyframe = keyframe;
			}
			else
			{
				_handles.Add( new KeyframeHandle( _timelineTrack, keyframe ) );
				_handles.Sort();
			}

			WriteToTrack();
			return true;
		}

		public bool RemoveAll( Predicate<KeyframeHandle> match )
		{
			if ( _handles.RemoveAll( match ) <= 0 ) return false;

			WriteToTrack();
			return true;
		}

		public void UpdatePositions()
		{
			foreach ( var handle in _handles )
			{
				handle.UpdatePosition();
			}
		}

		public void ReadFromTrack()
		{
			foreach ( var handle in _handles )
			{
				handle.Destroy();
			}

			_handles.Clear();

			foreach ( var keyframe in View.Keyframes )
			{
				_handles.Add( new KeyframeHandle( _timelineTrack, keyframe ) );
			}

			_handles.Sort();

			// Blocks that keyframes could apply a local (additive editing) effect to

			_sourceBlocks.Clear();
			_sourceBlocks.AddRange( Track.Blocks
				.Where( x => x.Signal is not IKeyframeSignal )
				.Select( GetBlockWithoutKeyframes ) );

			// Keyframe blocks must be cut by these times
			// Offset start by epsilon so keyframes at the very start of an additive block won't
			// be included in that block, letting you join non-additive and additive keyframe blocks

			_cutTimes.Clear();
			_cutTimes.AddRange( _sourceBlocks
				.SelectMany( x => new[] { x.TimeRange.Start + MovieTime.Epsilon, x.TimeRange.End } )
				.Distinct() );
		}

		[field: ThreadStatic]
		private static List<Keyframe>? WriteToTrack_Block { get; set; }

		[field: ThreadStatic]
		private static List<IProjectPropertyBlock>? WriteToTrack_Blocks { get; set; }

		public void WriteToTrack()
		{
			// Handles might have moved, re-sort them

			_handles.Sort();

			// Keyframes inside a source block will be an additive operation on that block,
			// otherwise they'll produce a new keyframe-only block

			var block = WriteToTrack_Block ??= new List<Keyframe>();
			var blocks = WriteToTrack_Blocks ??= new List<IProjectPropertyBlock>();

			block.Clear();
			blocks.Clear();

			var prevCutTime = MovieTime.Zero;

			foreach ( var handle in _handles )
			{
				var cutTime = _cutTimes.LastOrDefault( x => x <= handle.Time );

				if ( cutTime != prevCutTime && block.Count > 0 )
				{
					blocks.Add( FinishBlock( block ) );
					block.Clear();

					prevCutTime = cutTime;
				}

				block.Add( handle.Keyframe );
			}

			if ( block.Count > 0 )
			{
				blocks.Add( FinishBlock( block ) );
			}

			Track.SetBlocks( blocks );
			View.MarkValueChanged();
		}

		private static IProjectPropertyBlock GetBlockWithoutKeyframes( IProjectPropertyBlock block )
		{
			return block.Signal is IAdditiveSignal { First: { } source, Second: IKeyframeSignal }
				? block.WithSignal( source )
				: block;
		}

		private IProjectPropertyBlock FinishBlock( IReadOnlyList<Keyframe> keyframes )
		{
			var start = keyframes[0].Time;
			var end = keyframes[^1].Time;

			var sourceBlock = _sourceBlocks.FirstOrDefault( x => x.TimeRange.Grow( -MovieTime.Epsilon ).Contains( start ) );
			var propertyType = Track.TargetType;

			return sourceBlock?.WithSignal( PropertySignal.FromKeyframes( propertyType, keyframes, sourceBlock.Signal ) )
				?? PropertyBlock.FromSignal( PropertySignal.FromKeyframes( propertyType, keyframes ), (start, end) );
		}

		public IEnumerator<KeyframeHandle> GetEnumerator() => _handles.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
