
using Sandbox.Diagnostics;
using Sandbox.MovieMaker.Tracks;
using System.Linq;

namespace Editor.MovieMaker;

public class DopesheetTrack : GraphicsItem
{
	public TrackWidget Track;
	IEnumerable<DopeHandle> Handles => Children.OfType<DopeHandle>();

	public Color HandleColor { get; private set; }

	public DopesheetTrack( TrackWidget track ) : base()
	{
		Track = track;
		HoverEvents = true;

		HandleColor = Theme.Grey;

		if ( Track.Source is PropertyVector3Track )
		{
			HandleColor = Theme.Blue;
		}

		if ( Track.Source is PropertyRotationTrack )
		{
			HandleColor = Theme.Green;
		}

		if ( Track.Source is PropertyColorTrack )
		{
			HandleColor = Theme.Pink;
		}

		if ( Track.Source is PropertyFloatTrack )
		{
			HandleColor = Theme.Yellow;
		}
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		Paint.SetBrushAndPen( TrackDopesheet.Colors.ChannelBackground );
		Paint.DrawRect( LocalRect );
	}

	public void PositionHandles()
	{
		foreach ( var handle in Handles )
		{
			handle.UpdatePosition();
		}

		Update();
	}

	internal void DoLayout( Rect r )
	{
		Position = new Vector2( 0, r.Top + 1 );
		Size = new Vector2( 50000, r.Height );

		PositionHandles();
	}

	internal void OnSelected()
	{
		if ( Track.Source is PropertyTrack pt )
		{
			SceneEditorSession.Active.Selection.Set( pt.GameObject );
		}
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		base.OnMousePressed( e );

		if ( e.LeftMouseButton )
		{
			OnSelected();
		}
	}

	internal void AddKey( float time )
	{
		object value = null;

		if ( Track.Source is PropertyTrack pt )
		{
			value = pt.ReadCurrentValue();
		}

		AddKey( time, value );
	}

	internal void AddKey( float currentPointer, object value )
	{
		var h = Handles.Where( x => MathX.AlmostEqual( x.Time, currentPointer ) ).FirstOrDefault();

		if ( h is null )
		{
			h = new DopeHandle( this );

			//EditorUtility.PlayRawSound( "sounds/editor/add.wav" );
		}

		h.Time = currentPointer;
		h.Value = value;

		h.UpdatePosition();
	}

	/// <summary>
	/// Read from the Clip
	/// </summary>
	public void Read()
	{
		foreach ( var h in Handles )
		{
			h.Destroy();
		}

		if ( Track.Source is PropertyTrack provider )
		{
			var frames = provider.ReadFrames();
			Assert.NotNull( frames, "Frames returned null!" );
			for ( int i = 0; i < frames.Length; i++ )
			{
				var h = new DopeHandle( this );
				h.Time = frames[i].time;
				h.Value = frames[i].value;
			}
		}

		PositionHandles();
	}

	/// <summary>
	/// Write from this sheet to the target
	/// </summary>
	public void Write()
	{
		if ( Track.Source is not PropertyTrack pt )
			return;

		pt.WriteFrames( Handles.OrderBy( x => x.Time ).Select( x => new PropertyTrack.PropertyKeyframe { time = x.Time, value = x.Value } ).ToArray() );

	}
}
