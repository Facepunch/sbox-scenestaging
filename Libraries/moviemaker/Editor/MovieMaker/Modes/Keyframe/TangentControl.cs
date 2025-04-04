using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

public sealed class TangentControl : GraphicsItem
{
	public new TimelineTrack Parent { get; }
	public TrackView View { get; }

	public KeyframeHandle Target { get; }

	public KeyframeHandle? Prev { get; private set; }
	public KeyframeHandle? Next { get; private set; }

	public TangentControl( KeyframeHandle target )
		: base( target.Parent )
	{
		Parent = target.Parent;
		View = target.View;

		Target = target;
		ZIndex = 50;

		UpdateNeighbors();
		UpdatePosition();
	}

	private readonly List<KeyframeHandle> _handles = new();

	private void UpdateNeighbors()
	{
		_handles.Clear();
		_handles.AddRange( Target.Parent.Children.OfType<KeyframeHandle>() );
		_handles.Sort( ( a, b ) => a.Time.CompareTo( b.Time ) );

		var index = _handles.IndexOf( Target );

		if ( index == -1 )
		{
			Prev = null;
			Next = null;
			return;
		}

		Prev = index > 0 ? _handles[index - 1] : null;
		Next = index < _handles.Count - 1 ? _handles[index + 1] : null;

		if ( Prev?.Time == Target.Time ) Prev = null;
		if ( Next?.Time == Target.Time ) Next = null;
	}

	public void UpdatePosition()
	{
		PrepareGeometryChange();
		UpdateNeighbors();

		var left = (Prev ?? Target).Position.x;
		var right = (Next ?? Target).Position.x;

		Position = new Vector2( left, 0f );
		Size = new Vector2( right - left, Parent.Height );

		Update();
	}

	protected override void OnPaint()
	{
		var background = Theme.Primary.WithAlpha( 0.75f );

		var left = (Prev ?? Target).Position.x - Position.x;
		var center = Target.Position.x - Position.x;
		var right = (Next ?? Target).Position.x - Position.x;

		Paint.Antialiasing = true;

		Paint.SetPen( background.WithAlpha( 0.5f ), 0.5f );

		if ( Prev is { } prev )
		{
			var easing = Keyframe.GetInterpolationMode( prev.Keyframe.Interpolation, Target.Keyframe.Interpolation );

			Paint.SetBrushLinear( new Vector2( left, 0f ), new Vector2( center, 0f ), background.WithAlpha( 0.02f ), background );
			PaintExtensions.PaintCurve( t => easing.Apply( t ), new Rect( left, 0f, center - left, LocalRect.Height ), false, false );
		}

		if ( Next is { } next )
		{
			var easing = Keyframe.GetInterpolationMode( Target.Keyframe.Interpolation, next.Keyframe.Interpolation );

			Paint.SetBrushLinear( new Vector2( right, 0f ), new Vector2( center, 0f ), background.WithAlpha( 0.02f ), background );
			PaintExtensions.PaintCurve( t => easing.Apply( t ), new Rect( center, 0f, right - center, LocalRect.Height ), true, false );
		}
	}
}
