using System.Collections.Immutable;
using Sandbox.MovieMaker;
using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// A split view, with a list of tracks on the left and the dopesheet/curve view on the right
/// </summary>
public partial class TrackListWidget : Widget
{
	public MovieEditor Editor { get; }
	public Session Session { get; }

	private SceneEditorSession SceneEditorSession { get; }

	public DopeSheet DopeSheet { get; }

	public Widget LeftWidget { get; }
	public Widget RightWidget { get; }

	private readonly List<TrackWidget> _tracks = new();

	public IReadOnlyList<TrackWidget> Tracks => _tracks;

	private readonly ScrollArea _scrollArea;
	private readonly Layout _trackListLayout;

	private int _lastTrackHash;

	public TrackListWidget( MovieEditor parent ) : base( parent )
	{
		Session = parent.Session;
		Editor = parent;
		Layout = Layout.Column();

		SceneEditorSession = SceneEditorSession.Resolve( Session.Player.Scene );
		SceneEditorSession.Selection.OnItemAdded += OnSelectionAdded;

		_scrollArea = new ScrollArea( this );
		var splitter = new Splitter( _scrollArea );

		{
			LeftWidget = new Widget( this );
			splitter.AddWidget( LeftWidget );

			var leftLayout = Layout.Column();
			leftLayout.AddSpacingCell( 24 );

			var trackListWidget = leftLayout.Add( new Widget( this ) );
			trackListWidget.VerticalSizeMode = SizeMode.CanShrink;
			trackListWidget.MinimumWidth = 256;
			trackListWidget.Layout = Layout.Column();
			trackListWidget.Layout.Spacing = 8f;
			trackListWidget.Layout.Margin = new Sandbox.UI.Margin( 16, 0, 0, 16f );

			_trackListLayout = trackListWidget.Layout;

			LeftWidget.Layout = leftLayout;

			leftLayout.AddStretchCell();
		}

		{
			RightWidget = new Widget( this );
			splitter.AddWidget( RightWidget );

			RightWidget.Layout = Layout.Column();
			DopeSheet = RightWidget.Layout.Add( new DopeSheet( this ), 1 );
		}

		splitter.SetCollapsible( 0, false );
		splitter.SetStretch( 0, 1 );
		splitter.SetCollapsible( 1, false );
		splitter.SetStretch( 1, 3 );

		_scrollArea.Canvas = splitter;
		Layout.Add( _scrollArea );

		MouseTracking = true;
		AcceptDrops = true;

		Load( Session.Project );
	}

	private void OnSelectionAdded( object item )
	{
		if ( Tracks.Any( x => x.IsFocused ) || DopeSheet.IsFocused ) return;

		if ( item is GameObject go && Tracks.FirstOrDefault( x => x.Target is ITrackReference<GameObject> { IsBound: true } && x.Target.Value == go ) is { } track )
		{
			track.Focus( false );
			_scrollArea.MakeVisible( track );
		}
	}

	public override void OnDestroyed()
	{
		SceneEditorSession.Selection.OnItemAdded -= OnSelectionAdded;
	}

	private void Load( MovieProject project )
	{
		RebuildTracks();
	}

	/// <summary>
	/// Called when a track was added or removed
	/// </summary>
	void RebuildTracks()
	{
		foreach ( var track in Tracks )
		{
			if ( track.DopeSheetTrack is { } channel )
			{
				Session.EditMode?.TrackRemoved( channel );
			}
		}

		_trackListLayout.Clear( true );
		_tracks.Clear();

		_lastTrackHash = GetTrackHash();

		var groups = new Dictionary<IProjectTrack, TrackGroup>();

		foreach ( var track in Session.Project.Tracks )
		{
			var editorTrack = AddTrack( track );

			var parentGroup = track.Parent is null ? null : groups!.GetValueOrDefault( track.Parent );

			if ( track.Children.Count == 0 )
			{
				(parentGroup?.Content ?? _trackListLayout).Add( editorTrack );
				continue;
			}

			var group = new TrackGroup( editorTrack );

			groups[track] = group;

			(parentGroup?.Content ?? _trackListLayout).Add( group );
		}

		foreach ( var group in groups.Values )
		{
			group.UpdateCollapsedState();
		}

		DopeSheet.UpdateTracks();
	}

	private int GetTrackHash()
	{
		var hashCode = new HashCode();

		foreach ( var track in Session.Project.Tracks )
		{
			hashCode.Add( track );
		}

		return hashCode.ToHashCode();
	}

	/// <summary>
	/// Check tracks hash, rebuild if needed
	/// </summary>
	public void RebuildTracksIfNeeded()
	{
		if ( GetTrackHash() == _lastTrackHash ) return;

		RebuildTracks();
	}

	public TrackWidget? FindTrack( IProjectTrack track )
	{
		return Tracks.FirstOrDefault( x => x.ProjectTrack == track );
	}

	public TrackWidget AddTrack( IProjectTrack track )
	{
		var trackWidget = new TrackWidget( track, this );

		_tracks.Add( trackWidget );

		return trackWidget;
	}

	protected override void OnVisibilityChanged( bool visible )
	{
		base.OnVisibilityChanged( visible );

		if ( visible )
		{
			DopeSheet?.UpdateTracks();
		}
	}

	internal bool OnCanvasWheel( WheelEvent e )
	{
		// scoll
		if ( e.HasShift )
		{
			Session.ScrollBy( -e.Delta / 10.0f * (Session.PixelsPerSecond / 10.0f), true );
			DopeSheet?.UpdateTracks();
			return true;
		}

		// zoom
		if ( e.HasCtrl )
		{
			Session.Zoom( e.Delta / 10.0f );
			Update();
			DopeSheet?.UpdateTracks();
			return true;
		}

		return false;
	}

	public void ScrollBy( float x )
	{
		Session.ScrollBy( x, false );
		Update();
		DopeSheet?.UpdateTracks();
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;

		Paint.SetBrushAndPen( DopeSheet.Colors.Background.WithAlpha( 0.75f ) );
		Paint.DrawRect( LocalRect );
	}

	private IReadOnlyList<IProjectTrack>? _previewTracks;

	private IEnumerable<IProjectTrack> GetDraggedTracks( DragEvent ev )
	{
		if ( ev.Data.OfType<GameObject>().FirstOrDefault() is { } go )
		{
			yield return Session.GetOrCreateTrack( go );
			yield return Session.GetOrCreateTrack( go, nameof(GameObject.LocalPosition) );
			yield return Session.GetOrCreateTrack( go, nameof(GameObject.LocalRotation) );
		}

		if ( ev.Data.OfType<Component>().FirstOrDefault() is { } component )
		{
			yield return Session.GetOrCreateTrack( component );

			if ( component is SkinnedModelRenderer skinnedRenderer )
			{
				if ( skinnedRenderer.Parameters.Graph is { } graph )
				{
					for ( var i = 0; i < graph.ParamCount; ++i )
					{
						var paramName = graph.GetParameterName( i );

						yield return Session.GetOrCreateTrack( component, $"{nameof( SkinnedModelRenderer.Parameters )}.{paramName}" );
					}
				}

				foreach ( var morphName in skinnedRenderer.Morphs.Names )
				{
					yield return Session.GetOrCreateTrack( component, $"{nameof(SkinnedModelRenderer.Morphs)}.{morphName}" );
				}
			}
		}

		if ( ev.Data.OfType<SerializedProperty>().FirstOrDefault() is { } property )
		{
			if ( property.Parent.Targets?.FirstOrDefault() is Component parentComponent )
			{
				yield return Session.GetOrCreateTrack( parentComponent, property.Name );
			}
		}
	}

	public override void OnDragHover( DragEvent ev )
	{
		if ( _previewTracks is null )
		{
			var knownTracks = Session.Project.Tracks.ToImmutableHashSet();
			var dragged = GetDraggedTracks( ev ).ToImmutableHashSet();

			_previewTracks = dragged.Except( knownTracks ).ToArray();

			RebuildTracksIfNeeded();
		}

		ev.Action = _previewTracks.Count > 0
			? DropAction.Link
			: DropAction.Ignore;
	}

	public override void OnDragLeave()
	{
		if ( _previewTracks is { Count: > 0 } )
		{
			foreach ( var track in _previewTracks )
			{
				track.Remove();
			}

			RebuildTracksIfNeeded();
		}

		_previewTracks = null;
	}

	public override void OnDragDrop( DragEvent ev )
	{
		_previewTracks = null;
	}
}
