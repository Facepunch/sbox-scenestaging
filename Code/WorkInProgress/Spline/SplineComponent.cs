using Sandbox.Spline;

namespace Sandbox;
public sealed class SplineComponent : Component, Component.ExecuteInEditor
{
	[Property, Hide]
	private List<Spline.SplinePoint> _splinePoints = new()
	{
		new Spline.SplinePoint
		{
			Location = new Vector3( 0, 0, 0 ),
			InLocationRelative = new Vector3( -100, 0, 0 ),
			OutLocationRelative = new Vector3( 100, 0, 0 )
		},
		new Spline.SplinePoint
		{
			Location = new Vector3( 1000, 0, 0 ),
			InLocationRelative = new Vector3( -200, -200, 0 ),
			OutLocationRelative = new Vector3( 200, 200, 0 )
		},
		new Spline.SplinePoint
		{
			Location = new Vector3( 1000, 1000, 0 ),
			InLocationRelative = new Vector3( 0, -100, 0 ),
			OutLocationRelative = new Vector3( 0, 100, 0 )
		},
		new Spline.SplinePoint
		{
			Location = new Vector3( 0, 1000, 0 ),
			InLocationRelative = new Vector3( 100, 0, 0 ),
			OutLocationRelative = new Vector3( -100, 0, 0 )
		},
		new Spline.SplinePoint
		{
			Location = new Vector3( 0, 0, 0 ),
			InLocationRelative = new Vector3( -100, 0, 0 ),
			OutLocationRelative = new Vector3( 100, 0, 0 )
		}
	};

	private Spline.Utils.SplineSampler _splineSampler = new();

	private bool _isSampled = false;

	// TODO should be itnernal to editor only
	public void RequiresResample()
	{
		_isSampled = false;
	}

	// Usually you wont need to call this, it Will be called automatically when needed.
	// However, you can call this manually if you want ensure the sampling does not produce hitches
	// TODO honestly we should probably make this private it is very unlikely that this will cause performance issues / hitches
	// unless the spline has hundred points and is modified and accessed via distance accessors multiple times every frame
	// having this will just confuse people and lead to premature optimization
	public void Sample()
	{
		_splineSampler.Sample( _splinePoints.AsReadOnly() );
		_isSampled = true;
	}

	private void EnsureSplineIsSampled()
	{
		if ( _isSampled )
		{
			return;
		}
		Sample();
	}

	[Property]
	public bool IsLoop
	{
		get => Spline.Utils.IsLoop( _splinePoints.AsReadOnly() );
		set
		{
			// We emulate loops by adding an addtional point at the end which matches the first point
			// this might seem hacky at first but it makes things so much easier downstream,
			// because we can handle open splines and looped splines exactly the same when doing complex calculations
			// The fact that the last point exists will be hidden from the user in the Editor and most of the API
			if ( value )
			{
				_splinePoints.Add( _splinePoints[0] );
			}
			else
			{
				_splinePoints.RemoveAt( _splinePoints.Count - 1 );
			}
		}
	}

	public Vector3 GetLocationAtDistance( float distance )
	{
		EnsureSplineIsSampled();

		return Spline.Utils.GetLocation( _splinePoints.AsReadOnly(), _splineSampler.CalculateSegmentParamsAtDistance( distance ) );
	}

	public Vector3 GetTangetAtDistance( float distance )
	{
		EnsureSplineIsSampled();

		return Spline.Utils.GetTangent( _splinePoints.AsReadOnly(), _splineSampler.CalculateSegmentParamsAtDistance( distance ) );
	}

	public Vector3 GetTangent2DAtDistance( float distance )
	{
		EnsureSplineIsSampled();

		return Spline.Utils.GetTangent2D( _splinePoints.AsReadOnly(), _splineSampler.CalculateSegmentParamsAtDistance( distance ) );
	}

	public Vector3 GetNormalAtDistance( float distance, Vector3 up )
	{
		EnsureSplineIsSampled();

		return Spline.Utils.GetNormal( _splinePoints.AsReadOnly(), _splineSampler.CalculateSegmentParamsAtDistance( distance ), up );
	}

	public Vector3 GetNormal2DAtDistance( float distance )
	{
		EnsureSplineIsSampled();

		return Spline.Utils.GetNormal2D( _splinePoints.AsReadOnly(), _splineSampler.CalculateSegmentParamsAtDistance( distance ) );
	}

	public float GetCurvatureAtDistance( float distance )
	{
		EnsureSplineIsSampled();

		return Spline.Utils.GetCurvature( _splinePoints.AsReadOnly(), _splineSampler.CalculateSegmentParamsAtDistance( distance ) );
	}

	public float GetLength()
	{
		EnsureSplineIsSampled();

		return _splineSampler.TotalLength();
	}

	public float GetDistanceAtPoint( int pointIndex )
	{
		CheckPointIndex( pointIndex );
		EnsureSplineIsSampled();

		if ( pointIndex == _splinePoints.Count - 1 )
		{
			return _splineSampler.TotalLength();
		}
		return _splineSampler.GetSegmentStartDistance( pointIndex );
	}

	public float GetSegmentLength( int segmentIndex )
	{
		CheckSegmentIndex( segmentIndex );
		EnsureSplineIsSampled();

		return _splineSampler.GetSegmentLength( segmentIndex );
	}

	public SplinePoint GetPoint( int pointIndex )
	{
		CheckPointIndex( pointIndex );

		return _splinePoints[pointIndex];
	}

	public int NumberOfPoints()
	{
		return IsLoop ? _splinePoints.Count - 1 : _splinePoints.Count;
	}

	public int NumberOfSegments()
	{
		return Spline.Utils.SegmentNum( _splinePoints.AsReadOnly() );
	}

	public void UpdatePoint( int pointIndex, SplinePoint updatedPoint )
	{
		CheckPointIndex( pointIndex );

		_splinePoints[pointIndex] = updatedPoint;

		RecalculateTangentsForPointAndAdjacentPoints( pointIndex );

		RequiresResample();
	}

	public void InsertPoint( int pointIndex, SplinePoint newPoint )
	{
		CheckInsertPointIndex( pointIndex );

		_splinePoints.Insert( pointIndex, newPoint );

		RecalculateTangentsForPointAndAdjacentPoints( pointIndex );

		RequiresResample();
	}

	// returns index of new point
	public int AddPointAtDistance( float distance, Spline.Utils.SplitTangentModeBehavior tangentModeBehavior = Spline.Utils.SplitTangentModeBehavior.CustomCalculated )
	{
		EnsureSplineIsSampled();

		var splineParams = _splineSampler.CalculateSegmentParamsAtDistance( distance );
		_splinePoints = Spline.Utils.AddPoint( _splinePoints.AsReadOnly(), splineParams, tangentModeBehavior );

		var newPointIndex = splineParams.Index + 1;

		RecalculateTangentsForPointAndAdjacentPoints( newPointIndex );

		RequiresResample();

		return newPointIndex;
	}

	public void RemovePoint( int pointIndex )
	{
		CheckPointIndex( pointIndex );

		_splinePoints.RemoveAt( pointIndex );

		if ( pointIndex - 1 >= 0 )
		{
			RecalculateTangentsForPoint( pointIndex - 1 );
		}

		if ( pointIndex < _splinePoints.Count )
		{
			RecalculateTangentsForPoint( pointIndex );
		}

		RequiresResample();
	}

	// Returns a copy of the splines points
	// If the spline is looped the first and last point will be the same
	public List<SplinePoint> GetPoints()
	{
		return _splinePoints.ToList();
	}

	// Can be used to get information via GetLocationAtDistance and GetTangentAtDistance etc.
	// TODO maybe function that not only returns distance but also segment index, tangent, normal etc. as bundle
	public float FindDistanceClosestToLocation( Vector3 location )
	{
		EnsureSplineIsSampled();

		var splineParamsForClosestLocation = Spline.Utils.FindSegmentAndTClosestToLocation( _splinePoints.AsReadOnly(), location );

		return _splineSampler.GetDistanceAtSplineParams( splineParamsForClosestLocation );
	}

	private void CheckPointIndex( int pointIndex )
	{
		if ( pointIndex < 0 || pointIndex >= _splinePoints.Count || IsLoop && pointIndex == _splinePoints.Count - 1 )
		{
			throw new ArgumentOutOfRangeException( nameof( pointIndex ), "Spline point index out of range." );
		}
	}

	// Edge case: pointIndex > _splinePoints.Count
	private void CheckInsertPointIndex( int pointIndex )
	{
		if ( pointIndex < 0 || pointIndex > _splinePoints.Count )
		{
			throw new ArgumentOutOfRangeException( nameof( pointIndex ), "Spline point index out of range." );
		}
	}

	private void CheckSegmentIndex( int segmentIndex )
	{
		if ( segmentIndex < 0 || segmentIndex >= Spline.Utils.SegmentNum( _splinePoints.AsReadOnly() ) )
		{
			throw new ArgumentOutOfRangeException( nameof( segmentIndex ), "Spline segment index out of range." );
		}
	}

	// TODO should be editor internal only
	// maybe even make this a cross component functionality (mov to basec comp class)
	public bool ShouldRenderGizmos = true;

	protected override void DrawGizmos()
	{
		if ( !ShouldRenderGizmos )
			return;

		using ( Gizmo.Scope( "spline", new Transform( Vector3.Zero ) ) )
		{
			float lineThickness = 2f;

			// make line hitbox thicker to make it easier to hover/click.
			var potentialLineHit = DrawLineSegmentHitbox( lineThickness * 8f );
			var potentialPointHit = DrawPointHibtboxes();

			bool hovered = (potentialLineHit?.IsHovered ?? false) || (potentialPointHit?.IsHovered ?? false);

			if ( _splinePoints.Count > 1 )
			{
				DrawLineSegmentGizmo( hovered, lineThickness );
			}

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


	public struct SegmentHitResult
	{
		public float Distance;
		public bool IsHovered;
		public bool IsClicked;
	}
	///  TODO should be internal
	public SegmentHitResult? DrawLineSegmentHitbox( float thickness )
	{
		var polyline = Spline.Utils.ConvertSplineToPolyLine( _splinePoints.AsReadOnly(), 0.1f );
		SegmentHitResult result = new SegmentHitResult();

		using ( Gizmo.Scope( "curve_hitbox" ) )
		using ( Gizmo.Hitbox.LineScope() )
		{
			Gizmo.Draw.LineThickness = thickness;

			for ( var i = 0; i < polyline.Count - 1; i++ )
			{
				Gizmo.Hitbox.AddPotentialLine( polyline[i], polyline[i + 1], Gizmo.Draw.LineThickness );

				if ( Gizmo.IsHovered && Gizmo.HasMouseFocus )
				{
					if ( new Line( polyline[i], polyline[i + 1] ).ClosestPoint(
							Gizmo.CurrentRay.ToLocal( Gizmo.Transform ), out Vector3 point_on_line, out _ ) )
					{
						result.Distance = FindDistanceClosestToLocation( point_on_line );
					}

					result.IsHovered = Gizmo.IsHovered;
					result.IsClicked = Gizmo.HasClicked && Gizmo.Pressed.This;
				}
			}
		}

		return result;
	}

	///  TODO should be internal
	public void DrawLineSegmentGizmo( bool isHovered, float thickness )
	{
		if ( isHovered )
		{
			Gizmo.Draw.Color = Color.Orange;
		}

		Gizmo.Draw.LineThickness = thickness;

		using ( Gizmo.Scope( "curve_gizmo" ) )
		{
			var polyline = Spline.Utils.ConvertSplineToPolyLine( _splinePoints.AsReadOnly(), 0.1f );

			for ( var i = 0; i < polyline.Count - 1; i++ )
			{
				Gizmo.Draw.Line( polyline[i], polyline[i + 1] );
			}
		}
	}

	public struct PointHitResult
	{
		public enum HitTarget
		{
			None,
			Point,
			InTangent,
			OutTangent
		}

		public int PointIndex;
		public bool IsHovered;
		public bool IsClicked;

		public HitTarget Target;
	}
	///  TODO should be internal
	public PointHitResult? DrawPointHibtboxes()
	{
		using ( Gizmo.Scope( "point_hitbox" ) )
		{
			for ( var i = 0; i < _splinePoints.Count; i++ )
			{
				if ( !IsLoop || i != _splinePoints.Count - 1 )
				{
					var splinePoint = _splinePoints[i];

					using ( Gizmo.Scope( "point_hitbox" + i, new Transform( splinePoint.Location ) ) )
					using ( Gizmo.GizmoControls.PushFixedScale() )
					{
						Gizmo.Hitbox.BBox( BBox.FromPositionAndSize( Vector3.Zero, 2f ) );

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
		}

		return null;
	}
	private void DrawPointGizmos( bool isHovered )
	{
		for ( var i = 0; i < _splinePoints.Count; i++ )
		{
			if ( !IsLoop || i != _splinePoints.Count - 1 )
			{
				DrawPointGizmo( i, isHovered );
			}
		}
	}

	// should be internal
	public void DrawPointGizmo( int pointIndex, bool isHovered )
	{
		CheckPointIndex( pointIndex );

		var splinePoint = _splinePoints[pointIndex];

		using ( Gizmo.Scope( "point_gizmo" + pointIndex, new Transform( splinePoint.Location ) ) )
		using ( Gizmo.GizmoControls.PushFixedScale() )
		{
			if ( isHovered )
			{
				Gizmo.Draw.Color = Color.Orange;
			}
			Gizmo.Draw.SolidBox( BBox.FromPositionAndSize( Vector3.Zero, 2f ) );
		}
	}

	// TODO should be internal
	public void RecalculateTangentsForPointAndAdjacentPoints( int pointIndex )
	{
		RecalculateTangentsForPoint( pointIndex );
		if ( pointIndex > 0 )
		{
			RecalculateTangentsForPoint( pointIndex - 1 );
		}

		if ( pointIndex < _splinePoints.Count - 1 )
		{
			RecalculateTangentsForPoint( pointIndex + 1 );
		}

		if ( IsLoop )
		{
			if ( pointIndex == 0 )
			{
				RecalculateTangentsForPoint( _splinePoints.Count - 2 );
			}

			if ( pointIndex == _splinePoints.Count - 2 )
			{
				RecalculateTangentsForPoint( 0 );
			}
		}
	}

	private void RecalculateTangentsForPoint( int index )
	{
		if ( IsLoop && index == _splinePoints.Count - 1 )
		{
			index = 0;
		}
		switch ( _splinePoints[index].TangentMode )
		{
			case Spline.SplinePointTangentMode.Auto:
				_splinePoints[index] = Spline.Utils.CalculateSmoothTangentForPoint( _splinePoints.AsReadOnly(), index );
				break;
			case Spline.SplinePointTangentMode.Linear:
				_splinePoints[index] = Spline.Utils.CalculateLinearTangentForPoint( _splinePoints.AsReadOnly(), index );
				break;
			case Spline.SplinePointTangentMode.Custom:
				break;
			case Spline.SplinePointTangentMode.CustomMirrored:
				_splinePoints[index] = _splinePoints[index] with { OutLocationRelative = -_splinePoints[index].InLocationRelative };
				break;
		}
		if ( IsLoop && index == 0 )
		{
			_splinePoints[_splinePoints.Count - 1] = _splinePoints[0];
		}
	}
}
