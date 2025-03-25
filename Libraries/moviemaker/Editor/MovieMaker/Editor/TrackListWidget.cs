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
	public Session Session { get; }

	private SceneEditorSession SceneEditorSession { get; }

	public IEnumerable<TrackWidget> RootTracks => Children.OfType<TrackWidget>();
	public IEnumerable<TrackWidget> Tracks => RootTracks.SelectMany( EnumerateDescendants );

	private static IEnumerable<TrackWidget> EnumerateDescendants( TrackWidget track ) =>
		[track, ..track.Children.SelectMany( EnumerateDescendants )];

	private int _lastTrackHash;

	public TrackListWidget( ScrollArea parent, Session session )
		: base( parent )
	{
		Session = session;
		Layout = Layout.Column();

		SceneEditorSession = SceneEditorSession.Resolve( Session.Player.Scene );
		SceneEditorSession.Selection.OnItemAdded += OnSelectionAdded;

		MouseTracking = true;
		AcceptDrops = true;

		Load( Session.Project );
	}

	private void OnSelectionAdded( object item )
	{
		if ( Tracks.Any( x => x.IsFocused ) || Session.Editor.DopeSheetPanel!.DopeSheet.IsFocused ) return;
		if ( item is not GameObject go ) return;
		if ( Tracks.FirstOrDefault( x => x.View.Target is ITrackReference<GameObject> { IsBound: true } target && target.Value == go ) is not { } track ) return;
		
		track.Focus( false );

		if ( Parent is ScrollArea scrollArea )
		{
			scrollArea.MakeVisible( track );
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
		Layout.Clear( true );

		_lastTrackHash = GetTrackHash();

		foreach ( var track in Session.TrackList.RootTracks )
		{
			Layout.Add( new TrackWidget( this, null, track ) );
		}
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
		return Tracks.FirstOrDefault( x => x.View.Track == track );
	}

	internal bool OnCanvasWheel( WheelEvent e )
	{
		// scoll
		if ( e.HasShift )
		{
			Session.ScrollBy( -e.Delta / 10.0f * (Session.PixelsPerSecond / 10.0f), true );
			return true;
		}

		// zoom
		if ( e.HasCtrl )
		{
			Session.Zoom( e.Delta / 10.0f );
			Update();
			return true;
		}

		return false;
	}

	public void ScrollBy( float x )
	{
		Session.ScrollBy( x, false );
		Update();
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
			yield return Session.GetOrCreateTrack( go, nameof(GameObject.Enabled) );
			yield return Session.GetOrCreateTrack( go, nameof(GameObject.LocalPosition) );
			yield return Session.GetOrCreateTrack( go, nameof(GameObject.LocalRotation) );

			if ( go.GetComponent<PlayerController>() is { } controller )
			{
				yield return Session.GetOrCreateTrack( controller );
				yield return Session.GetOrCreateTrack( controller, nameof(PlayerController.EyeAngles) );
				yield return Session.GetOrCreateTrack( controller, nameof(PlayerController.WishVelocity) );
				yield return Session.GetOrCreateTrack( controller, nameof(PlayerController.IsSwimming) );
				yield return Session.GetOrCreateTrack( controller, nameof(PlayerController.IsClimbing) );
				yield return Session.GetOrCreateTrack( controller, nameof(PlayerController.IsDucking) );
			}


			if ( go.GetComponent<Rigidbody>() is { } rigidBody )
			{
				yield return Session.GetOrCreateTrack( rigidBody );
				yield return Session.GetOrCreateTrack( rigidBody, nameof(Rigidbody.Velocity) );
			}
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
