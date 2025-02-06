using System.Numerics;

namespace Sandbox;

/// <summary>
/// Represents a spline component that can be manipulated within the editor and at runtime.
/// </summary>
public sealed class SplineComponent : Component, Component.ExecuteInEditor, Component.IHasBounds
{
	[Property, Hide]
	public SplineMath.Spline Spline = new();

	public SplineComponent()
	{
		Spline.InsertPoint( Spline.PointCount, new SplineMath.Spline.Point { Position = new Vector3( 0, 0, 0 ) } );
		Spline.InsertPoint( Spline.PointCount, new SplineMath.Spline.Point { Position = new Vector3( 100, 0, 0 ) } );
		Spline.InsertPoint( Spline.PointCount, new SplineMath.Spline.Point { Position = new Vector3( 100, 100, 0 ) } );
	}

	public BBox LocalBounds { get => Spline.Bounds; }

	protected override void OnEnabled()
	{
		Spline.SplineChanged += UpdateDrawCache;
		base.OnEnabled();
	}

	protected override void OnDisabled()
	{
		Spline.SplineChanged -= UpdateDrawCache;
		base.OnDisabled();
	}

	protected override void OnValidate()
	{
		UpdateDrawCache();
	}

	// TODO should be editor internal only
	// maybe even make this a cross component functionality (mov to basec comp class)
	public bool ShouldRenderGizmos {
		get => _shouldRenderGizmos;
		set {
			_shouldRenderGizmos = value;
		}
	}

	private bool _shouldRenderGizmos = true;

	private void UpdateDrawCache()
	{
		if ( Scene.IsEditor )
		{
			Spline.ConvertToPolyline( ref _drawCachePolyline );

			_drawCachePolylineLines.Clear();
			for ( var i = 0; i < _drawCachePolyline.Count - 1; i++ )
			{
				_drawCachePolylineLines.Add( new Line( _drawCachePolyline[i], _drawCachePolyline[i + 1] ) );
			}
		}
	}

	protected override void DrawGizmos()
	{
		if ( !ShouldRenderGizmos )
			return;

		// spline gizmos are expensive so we actually want to frustum cull them here already
		if ( !Gizmo.Camera.GetFrustum( Gizmo.Camera.Rect, 1 ).IsInside( Spline.Bounds.Transform(WorldTransform), true ) )
		{
			return;
		}

		using ( Gizmo.Scope( "spline" ) )
		{
			float lineThickness = 2f;

			if ( Spline.PointCount < 1 )
			{
				return;
			}

			// make line hitbox thicker to make it easier to hover/click.
			var potentialLineHit = DrawLineSegmentHitbox( lineThickness * 16f );
			var potentialPointHit = DrawPointHibtboxes();

			bool hovered = (potentialLineHit?.IsHovered ?? false) || (potentialPointHit?.IsHovered ?? false);

			DrawLineSegmentGizmo( hovered, lineThickness );

			DrawPointGizmos( hovered );

			if ( potentialLineHit?.IsClicked ?? false )
			{
				Gizmo.Select();
			}

			if ( potentialPointHit?.IsClicked ?? false )
			{
				Gizmo.Select();
			}
		}
	}

	private struct SegmentHitResult
	{
		public float Distance;
		public bool IsHovered;
		public bool IsClicked;
	}

	private List<Vector3> _drawCachePolyline = new();
	private List<Line> _drawCachePolylineLines = new();

	private SegmentHitResult? DrawLineSegmentHitbox( float thickness )
	{
		SegmentHitResult result = new SegmentHitResult();

		using ( Gizmo.Scope( "curve_hitbox" ) )
		using ( Gizmo.Hitbox.LineScope() )
		{
			Gizmo.Draw.LineThickness = thickness;

			for ( var i = 0; i < _drawCachePolyline.Count - 1; i++ )
			{
				Gizmo.Hitbox.AddPotentialLine( _drawCachePolyline[i], _drawCachePolyline[i + 1], Gizmo.Draw.LineThickness );

				if ( Gizmo.IsHovered && Gizmo.HasMouseFocus )
				{
					if ( new Line( _drawCachePolyline[i], _drawCachePolyline[i + 1] ).ClosestPoint(
							Gizmo.CurrentRay.ToLocal( Gizmo.Transform ), out Vector3 point_on_line, out _ ) )
					{
						result.Distance = Spline.SampleAtClosestPosition( point_on_line ).Distance;
					}

					result.IsHovered = Gizmo.IsHovered;
					result.IsClicked = Gizmo.HasClicked && Gizmo.Pressed.This;
				}
			}
		}

		return result;
	}

	private void DrawLineSegmentGizmo( bool isHovered, float thickness )
	{
		if ( isHovered )
		{
			Gizmo.Draw.Color = Color.Orange;
		}

		Gizmo.Draw.LineThickness = thickness;

		using ( Gizmo.Scope( "curve_gizmo" ) )
		{
			Gizmo.Draw.Lines( _drawCachePolylineLines );

			//DrawFrames();
		}
	}

	private void DrawFrames()
	{
		// Draw rotation-minimizing frames every 30 units, considering roll
		float totalLength = Spline.Length;

		var previousSample = Spline.SampleAtDistance( 0f );

		Vector3 previousTangent = previousSample.Tangent;

		// This has to be the dumbest way to find a perpendicular vector
		if ( previousTangent == Vector3.Up )
		{
			previousTangent = -Vector3.Forward;
		}
		else if ( previousTangent == -Vector3.Up )
		{
			previousTangent = Vector3.Forward;
		}
		else if ( previousTangent == Vector3.Zero )
		{
			previousTangent = Vector3.Forward;
		}
		Vector3 up = Vector3.Cross( previousTangent, new Vector3( -previousTangent.y, previousTangent.x, 0f ) ).Normal;

		Vector3 previousPosition = previousSample.Position;
		float previousRoll = previousSample.Roll;

		// Apply initial roll to up vector
		up = RotateVectorAroundAxis( up, previousTangent, MathX.DegreeToRadian( previousRoll ) );

		float step = 5f;
		for ( float distance = step; distance <= totalLength; distance += step )
		{
			var sample = Spline.SampleAtDistance( distance );
			Vector3 position = sample.Position;
			Vector3 tangent = sample.Scale;

			// Calculate rotation-minimizing frame using parallel transport
			Vector3 transportUp = ParallelTransport( up, previousTangent, tangent );

			// Get interpolated roll at the current distance
			float roll = sample.Roll;

			// Apply roll to the up vector
			float deltaRoll = roll - previousRoll;
			Vector3 finalUp = RotateVectorAroundAxis( transportUp, tangent, MathX.DegreeToRadian( deltaRoll ) );

			// Calculate right (binormal) vector
			Vector3 right = Vector3.Cross( tangent, finalUp ).Normal;

			float arrowLength = step * 1.5f;

			// Draw tangent vector (forward)
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.Arrow( position, position + tangent * arrowLength, arrowLength / 10f, arrowLength / 15f );

			// Draw up vector (normal)
			Gizmo.Draw.Color = Color.Green;
			Gizmo.Draw.Arrow( position, position + finalUp * arrowLength, arrowLength / 10f, arrowLength / 15f );

			// Draw right vector (binormal)
			Gizmo.Draw.Color = Color.Blue;
			Gizmo.Draw.Arrow( position, position + right * arrowLength, arrowLength / 10f, arrowLength / 15f );

			// Update previous vectors for the next iteration
			up = finalUp;
			previousTangent = tangent;
			previousRoll = roll;
		}
	}

	// Helper method to perform parallel transport of the up vector
	private Vector3 ParallelTransport( Vector3 up, Vector3 previousTangent, Vector3 currentTangent )
	{
		Vector3 rotationAxis = Vector3.Cross( previousTangent, currentTangent );
		float dotProduct = Vector3.Dot( previousTangent, currentTangent );
		float angle = MathF.Acos( Math.Clamp( dotProduct, -1f, 1f ) );

		if ( rotationAxis.LengthSquared > 0.0001f && angle > 0.0001f )
		{
			rotationAxis = rotationAxis.Normal;
			Quaternion rotation = Quaternion.CreateFromAxisAngle( rotationAxis, angle );
			up = System.Numerics.Vector3.Transform( up, rotation );
		}

		return up;
	}

	// Helper method to rotate a vector around an axis by an angle
	private Vector3 RotateVectorAroundAxis( Vector3 vector, Vector3 axis, float angle )
	{
		Quaternion rotation = Quaternion.CreateFromAxisAngle( axis, angle );
		return System.Numerics.Vector3.Transform( vector, rotation );
	}

	private struct PointHitResult
	{
		public int PointIndex;
		public bool IsHovered;
		public bool IsClicked;
	}
	private PointHitResult? DrawPointHibtboxes()
	{
		using ( Gizmo.Scope( "point_hitbox" ) )
		using ( Gizmo.GizmoControls.PushFixedScale() )
		{
			for ( var i = 0; i < Spline.PointCount; i++ )
			{
				if ( !Spline.IsLoop || i != Spline.PointCount - 1 )
				{
					var splinePoint = Spline.GetPoint( i );

					Gizmo.Hitbox.BBox( BBox.FromPositionAndSize( splinePoint.Position, 2f ) );

					if ( Gizmo.IsHovered )
					{
						PointHitResult result = new PointHitResult();

						result.IsHovered = Gizmo.IsHovered;
						result.IsClicked = Gizmo.HasClicked && Gizmo.Pressed.This;
						result.PointIndex = i;

						return result;
					}
				}
			}
		}

		return null;
	}
	private void DrawPointGizmos( bool isHovered )
	{
		for ( var i = 0; i < Spline.PointCount; i++ )
		{
			if ( !Spline.IsLoop || i != Spline.PointCount - 1 )
			{
				DrawPointGizmo( i, isHovered );
			}
		}
	}
	private void DrawPointGizmo( int pointIndex, bool isHovered )
	{
		var splinePoint = Spline.GetPoint( pointIndex );

		using ( Gizmo.Scope( "point_gizmo" + pointIndex, new Transform( splinePoint.Position ) ) )
		using ( Gizmo.GizmoControls.PushFixedScale() )
		{
			if ( isHovered )
			{
				Gizmo.Draw.Color = Color.Orange;
			}
			Gizmo.Draw.SolidBox( BBox.FromPositionAndSize( Vector3.Zero, 2f ) );
		}
	}
}
