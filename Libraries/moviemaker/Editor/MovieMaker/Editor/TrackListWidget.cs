using System.Collections.Immutable;
using Sandbox.MovieMaker;
using System.Linq;

namespace Editor.MovieMaker;

/// <summary>
/// A split view, with a list of tracks on the left and the dopesheet/curve view on the right
/// </summary>
public partial class TrackListWidget : Widget, EditorEvent.ISceneEdited
{
	public MovieEditor Editor { get; init; }
	public Session Session { get; private set; }

	private SceneEditorSession SceneEditorSession { get; }

	Layout TrackList;

	public DopeSheet DopeSheet { get; }

	public Widget LeftWidget { get; init; }
	public Widget RightWidget { get; init; }

	public List<TrackWidget> Tracks = new List<TrackWidget>();

	ScrollArea ScrollArea;

	private int _lastTrackHash;

	public TrackListWidget( MovieEditor parent ) : base( parent )
	{
		Session = parent.Session;
		Editor = parent;
		Layout = Layout.Column();

		SceneEditorSession = SceneEditorSession.Resolve( Session.Player.Scene );
		SceneEditorSession.Selection.OnItemAdded += OnSelectionAdded;

		ScrollArea = new ScrollArea( this );
		var splitter = new Splitter( ScrollArea );

		{
			var left = new Widget( this );
			splitter.AddWidget( left );

			var leftLayout = Layout.Column();
			leftLayout.AddSpacingCell( 24 );

			var trackListWidget = leftLayout.Add( new Widget( this ) );
			trackListWidget.VerticalSizeMode = SizeMode.CanShrink;
			trackListWidget.MinimumWidth = 256;
			trackListWidget.Layout = Layout.Column();
			trackListWidget.Layout.Spacing = 8f;
			trackListWidget.Layout.Margin = new Sandbox.UI.Margin( 16, 0, 0, 16f );

			TrackList = trackListWidget.Layout;

			left.Layout = leftLayout;

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

		ScrollArea.Canvas = splitter;
		Layout.Add( ScrollArea );

		MouseTracking = true;
		AcceptDrops = true;

		Load( Session.Clip );
	}

	private void OnSelectionAdded( object item )
	{
		if ( Tracks.Any( x => x.IsFocused ) || DopeSheet.IsFocused ) return;

		if ( item is GameObject go && Tracks.FirstOrDefault( x => x.Property is ISceneReferenceMovieProperty { IsBound: true } && x.Property.Value == go ) is { } track )
		{
			track.Focus( false );
			ScrollArea.MakeVisible( track );
		}
	}

	public override void OnDestroyed()
	{
		SceneEditorSession.Selection.OnItemAdded -= OnSelectionAdded;
	}

	void ScrubToTime( float time )
	{
		Session.Player.ApplyFrame( time );
	}

	private void Load( MovieClip clip )
	{
		if ( clip is null )
			return;

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

		TrackList.Clear( true );
		Tracks.Clear();

		if ( Session.Clip is { } clip )
		{
			_lastTrackHash = clip.TrackHash;

			var groups = new Dictionary<MovieTrack, TrackGroup>();

			foreach ( var track in clip.AllTracks )
			{
				var editorTrack = AddTrack( track );

				var parentGroup = track.Parent is null ? null : groups.GetValueOrDefault( track.Parent );

				if ( track.Children.Count == 0 )
				{
					(parentGroup?.Content ?? TrackList).Add( editorTrack );
					continue;
				}

				var group = new TrackGroup( editorTrack );

				groups[track] = group;

				(parentGroup?.Content ?? TrackList).Add( group );
			}
		}
		else
		{
			_lastTrackHash = 0;
		}

		DopeSheet.UpdateTracks();
	}

	/// <summary>
	/// Check tracks hash, rebuild if needed
	/// </summary>
	public void RebuildTracksIfNeeded()
	{
		if ( (Session.Clip?.TrackHash ?? 0) == _lastTrackHash ) return;

		RebuildTracks();
	}

	TrackWidget FindTrack( MovieTrack track )
	{
		return Tracks.FirstOrDefault( x => x.MovieTrack == track );
	}

	public TrackWidget AddTrack( MovieTrack track )
	{
		var trackWidget = new TrackWidget( track, this );

		Tracks.Add( trackWidget );

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

	public float MinTimeVisible => 5;
	public float MaxTimeVisible => 120;

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

	public void OnCopy()
	{
		DopeSheet?.OnCopy();
	}

	public void OnPaste()
	{
		DopeSheet?.OnPaste();
	}


	public void OnDelete()
	{
		DopeSheet?.OnDelete();
	}

	private IReadOnlyList<MovieTrack> _previewTracks;

	private IEnumerable<MovieTrack> GetDraggedTracks( DragEvent ev )
	{
		if ( Session?.Player is not { MovieClip: not null } player ) yield break;

		if ( ev.Data.OfType<GameObject>().FirstOrDefault() is { } go )
		{
			yield return player.GetOrCreateTrack( go );
			yield return player.GetOrCreateTrack( go, nameof(GameObject.LocalPosition) );
			yield return player.GetOrCreateTrack( go, nameof(GameObject.LocalRotation) );
		}

		if ( ev.Data.OfType<Component>().FirstOrDefault() is { } component )
		{
			yield return player.GetOrCreateTrack( component );

			if ( component is SkinnedModelRenderer skinnedRenderer )
			{
				if ( skinnedRenderer.Parameters.Graph is { } graph )
				{
					for ( var i = 0; i < graph.ParamCount; ++i )
					{
						var paramName = graph.GetParameterName( i );

						yield return player.GetOrCreateTrack( component, $"{nameof( SkinnedModelRenderer.Parameters )}.{paramName}" );
					}
				}

				foreach ( var morphName in skinnedRenderer.Morphs.Names )
				{
					yield return player.GetOrCreateTrack( component, $"{nameof(SkinnedModelRenderer.Morphs)}.{morphName}" );
				}
			}
		}

		if ( ev.Data.OfType<SerializedProperty>().FirstOrDefault() is { } property )
		{
			if ( property.Parent.Targets?.FirstOrDefault() is Component parentComponent )
			{
				yield return player.GetOrCreateTrack( parentComponent, property.Name );
			}
		}
	}

	public override void OnDragHover( DragEvent ev )
	{
		if ( _previewTracks is null && Session?.Player is { MovieClip: { } clip } )
		{
			var knownTracks = clip.AllTracks.ToImmutableHashSet();
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
