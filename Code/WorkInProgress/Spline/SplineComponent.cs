using Sandbox.Spline;

namespace Sandbox;

/// <summary>
/// Represents a spline component that can be manipulated within the editor and at runtime.
/// </summary>
public sealed class SplineComponent : Component, Component.ExecuteInEditor, Component.IHasBounds
{
	[Property, Hide]
	private List<Spline.SplinePoint> _splinePoints = new();

	[Property, Hide]
	private List<Spline.SplinePointTangentMode> _pointTangentModes = new();

	[Property, Hide]
	private List<float> _pointRolls = new();

	// X = Scale Width, Y = Scale Height
	[Property, Hide]
	private List<Vector2> _pointScales = new();

	public SplineComponent()
	{
		_splinePoints.Add( new SplinePoint { Position = new Vector3( 0, 0, 0 ) } );
		_splinePoints.Add( new SplinePoint { Position = new Vector3( 100, 0, 0 ) } );
		_splinePoints.Add( new SplinePoint { Position = new Vector3( 100, 100, 0 ) } );
		_pointTangentModes.Add( SplinePointTangentMode.Auto );
		_pointTangentModes.Add( SplinePointTangentMode.Auto );
		_pointTangentModes.Add( SplinePointTangentMode.Auto );
		_pointRolls.Add( 0 );
		_pointRolls.Add( 0 );
		_pointRolls.Add( 0 );
		_pointScales.Add( new Vector2( 1, 1 ) );
		_pointScales.Add( new Vector2( 1, 1 ) );
		_pointScales.Add( new Vector2( 1, 1 ) );

		RecalculateTangentsForPoint( 0 );
		RecalculateTangentsForPoint( 1 );
		RecalculateTangentsForPoint( 2 );
	}

	private Spline.Utils.SplineSampler _distanceSampler = new();

	private bool _areDistancesSampled = false;

	/// <summary>
	/// Gets a value indicating whether the spline needs resampling.
	/// </summary>
	public bool IsDirty => !_areDistancesSampled;

	/// <summary>
	/// An event that is invoked when the spline changes.
	/// </summary>
	public Action SplineChanged;

	public BBox LocalBounds { get => _distanceSampler.GetTotalBounds(); }

	protected override void OnValidate()
	{
		SampleDistances();
		UpdateDrawCache();
		SplineChanged?.Invoke();
	}

	private void RequiresDistanceResample()
	{
		_areDistancesSampled = false;
		SplineChanged?.Invoke();
	}

	private void SampleDistances()
	{
		_distanceSampler.Sample( _splinePoints.AsReadOnly() );
		_areDistancesSampled = true;
	}

	private void EnsureSplineIsDistanceSampled()
	{
		if ( _areDistancesSampled )
		{
			return;
		}
		SampleDistances();
		UpdateDrawCache();
	}

	/// <summary>
	/// Whether the spline forms a loop.
	/// </summary>
	[Property]
	public bool IsLoop
	{
		get => Spline.Utils.IsLoop( _splinePoints.AsReadOnly() );
		set
		{
			var isAlreadyLoop = Spline.Utils.IsLoop( _splinePoints.AsReadOnly());
			// We emulate loops by adding an addtional point at the end which matches the first point
			// this might seem hacky at first but it makes things so much easier downstream,
			// because we can handle open splines and looped splines exactly the same when doing complex calculations
			// The fact that the last point exists will be hidden from the user in the Editor and API
			if ( value && !isAlreadyLoop )
			{
				_splinePoints.Add( _splinePoints[0] );
				_pointTangentModes.Add( _pointTangentModes[0] );
				_pointRolls.Add( _pointRolls[0] );
				_pointScales.Add( _pointScales[0] );
				RequiresDistanceResample();
			}
			else if ( isAlreadyLoop ) // only remove the last point if we are actually a loop
			{
				_splinePoints.RemoveAt( _splinePoints.Count - 1 );
				_pointTangentModes.RemoveAt( _pointTangentModes.Count - 1 );
				_pointRolls.RemoveAt( _pointRolls.Count - 1 );
				_pointScales.RemoveAt( _pointScales.Count - 1 );
				RequiresDistanceResample();
			}
		}
	}

	public Vector3 GetPositionAtDistance( float distance )
	{
		EnsureSplineIsDistanceSampled();

		return Spline.Utils.GetPosition( _splinePoints.AsReadOnly(), _distanceSampler.CalculateSegmentParamsAtDistance( distance ) );
	}

	public Vector3 GetTangetAtDistance( float distance )
	{
		EnsureSplineIsDistanceSampled();

		return Spline.Utils.GetTangent( _splinePoints.AsReadOnly(), _distanceSampler.CalculateSegmentParamsAtDistance( distance ) );
	}

	public Vector3 GetTangent2DAtDistance( float distance )
	{
		EnsureSplineIsDistanceSampled();

		return Spline.Utils.GetTangent2D( _splinePoints.AsReadOnly(), _distanceSampler.CalculateSegmentParamsAtDistance( distance ) );
	}

	public Vector3 GetNormalAtDistance( float distance, Vector3 up )
	{
		EnsureSplineIsDistanceSampled();

		return Spline.Utils.GetNormal( _splinePoints.AsReadOnly(), _distanceSampler.CalculateSegmentParamsAtDistance( distance ), up );
	}

	public Vector3 GetNormal2DAtDistance( float distance )
	{
		EnsureSplineIsDistanceSampled();

		return Spline.Utils.GetNormal2D( _splinePoints.AsReadOnly(), _distanceSampler.CalculateSegmentParamsAtDistance( distance ) );
	}

	public float GetCurvatureAtDistance( float distance )
	{
		EnsureSplineIsDistanceSampled();

		return Spline.Utils.GetCurvature( _splinePoints.AsReadOnly(), _distanceSampler.CalculateSegmentParamsAtDistance( distance ) );
	}

	public float GetRollAtDistance( float distance )
    {
        EnsureSplineIsDistanceSampled();
		var splineParams = _distanceSampler.CalculateSegmentParamsAtDistance( distance );
		var distanceAlongSegment = distance - _distanceSampler.GetSegmentStartDistance( splineParams.Index );
		var segmentLength = _distanceSampler.GetSegmentLength( splineParams.Index );
		return MathX.Lerp( _pointRolls[splineParams.Index], _pointRolls[splineParams.Index + 1], distanceAlongSegment / segmentLength );
	}

	public Vector2 GetScaleAtDistance( float distance )
	{
		EnsureSplineIsDistanceSampled();
		var splineParams = _distanceSampler.CalculateSegmentParamsAtDistance( distance );
		var distanceAlongSegment = distance - _distanceSampler.GetSegmentStartDistance( splineParams.Index );
		var segmentLength = _distanceSampler.GetSegmentLength( splineParams.Index );
		return Vector2.Lerp( _pointScales[splineParams.Index], _pointScales[splineParams.Index + 1], distanceAlongSegment / segmentLength );
	}

	public float GetLength()
	{
		EnsureSplineIsDistanceSampled();

		return _distanceSampler.TotalLength();
	}

	public BBox GetBounds()
	{
		EnsureSplineIsDistanceSampled();

		return _distanceSampler.GetTotalBounds();
	}

	public float GetDistanceAtPoint( int pointIndex )
	{
		CheckPointIndex( pointIndex );
		EnsureSplineIsDistanceSampled();

		if ( pointIndex == _splinePoints.Count - 1 )
		{
			return _distanceSampler.TotalLength();
		}
		return _distanceSampler.GetSegmentStartDistance( pointIndex );
	}

	public float GetSegmentLength( int segmentIndex )
	{
		CheckSegmentIndex( segmentIndex );
		EnsureSplineIsDistanceSampled();

		return _distanceSampler.GetSegmentLength( segmentIndex );
	}

	public BBox GetSegmentBouds( int segmentIndex )
	{
		CheckSegmentIndex( segmentIndex );
		EnsureSplineIsDistanceSampled();

		return _distanceSampler.GetSegmentBounds( segmentIndex );
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

		RequiresDistanceResample();
	}

	public void InsertPoint( int pointIndex, SplinePoint newPoint )
	{
		CheckInsertPointIndex( pointIndex );

		_splinePoints.Insert( pointIndex, newPoint );
		_pointTangentModes.Insert( pointIndex, SplinePointTangentMode.Auto );
		_pointRolls.Insert( pointIndex, 0 );
		_pointScales.Insert( pointIndex, new Vector2( 1, 1 ) );

		RecalculateTangentsForPointAndAdjacentPoints( pointIndex );

		RequiresDistanceResample();
	}

	/// <summary>
	/// Adds a point at a specific distance along the spline.
	/// Returns the index of the added spline point.
	/// Tangents of the new point and adjacent poinrs will be calculated so the spline shape remains the same.
	/// </summary>
	public int AddPointAtDistance( float distance, bool inferTangentModes = false )
	{
		EnsureSplineIsDistanceSampled();

		var splineParams = _distanceSampler.CalculateSegmentParamsAtDistance( distance );

		var positionSplitResult = Spline.Utils.SplitSegment( _splinePoints.AsReadOnly(), splineParams );

		// modify points before and after the split
		_splinePoints[splineParams.Index] = positionSplitResult.Left;
		_splinePoints[splineParams.Index + 1] = positionSplitResult.Right;

		_splinePoints.Insert( splineParams.Index + 1, positionSplitResult.Mid );


		if ( inferTangentModes )
		{
			var splinePointTangentMode = InferTangentModeForSplitPoint( splineParams );
			_pointTangentModes.Insert( splineParams.Index + 1, splinePointTangentMode );
		}
		else
		{
			_pointTangentModes[splineParams.Index] = SplinePointTangentMode.Split;
			_pointTangentModes[splineParams.Index + 1] = SplinePointTangentMode.Split;

			_pointTangentModes.Insert( splineParams.Index + 1, SplinePointTangentMode.Split );
		}

		// split scale and roll
		_pointRolls.Insert( splineParams.Index + 1, GetRollAtDistance( distance ) );
		_pointScales.Insert( splineParams.Index + 1, GetScaleAtDistance( distance ) );

		var newPointIndex = splineParams.Index + 1;

		RecalculateTangentsForPointAndAdjacentPoints( newPointIndex );

		RequiresDistanceResample();

		return newPointIndex;
	}

	private SplinePointTangentMode InferTangentModeForSplitPoint( SplineSegmentParams segmentParams )
	{
		// If the tangent modes are the same on both sides we just assume the new points should have the same tangent mode
		if ( _pointTangentModes[segmentParams.Index] == _pointTangentModes[segmentParams.Index + 1] )
		{
			return _pointTangentModes[segmentParams.Index];
		}

		// if one of them uses auto asume the new points should use auto
		if ( _pointTangentModes[segmentParams.Index] == SplinePointTangentMode.Auto ||
			 _pointTangentModes[segmentParams.Index + 1] == SplinePointTangentMode.Auto )
		{
			return SplinePointTangentMode.Auto;
		}

		// If one of them uses linear assume the new points should use linear
		if ( _pointTangentModes[segmentParams.Index] == SplinePointTangentMode.Linear ||
			 _pointTangentModes[segmentParams.Index + 1] == SplinePointTangentMode.Linear )
		{
			return SplinePointTangentMode.Linear;
		}

		// Otherwise we default to custom
		return SplinePointTangentMode.Split;
	}


	public void RemovePoint( int pointIndex )
	{
		CheckPointIndex( pointIndex );

		_splinePoints.RemoveAt( pointIndex );
		_pointTangentModes.RemoveAt( pointIndex );
		_pointRolls.RemoveAt( pointIndex );
		_pointScales.RemoveAt( pointIndex );

		if ( pointIndex - 1 >= 0 )
		{
			RecalculateTangentsForPoint( pointIndex - 1 );
		}

		if ( pointIndex < _splinePoints.Count )
		{
			RecalculateTangentsForPoint( pointIndex );
		}

		RequiresDistanceResample();
	}

	public float GetRollForPoint( int pointIndex )
	{
		CheckPointIndex( pointIndex );
		return _pointRolls[pointIndex];
	}

	public void SetRollForPoint( int pointIndex, float roll )
	{
		CheckPointIndex( pointIndex );
		_pointRolls[pointIndex] = roll;
		SplineChanged?.Invoke();
	}

	public Vector2 GetScaleForPoint( int pointIndex )
	{
		CheckPointIndex( pointIndex );
		return _pointScales[pointIndex];
	}
	public void SetScaleForPoint( int pointIndex, Vector2 scale )
	{
		CheckPointIndex( pointIndex );
		_pointScales[pointIndex] = scale;
		SplineChanged?.Invoke();
	}

	public SplinePointTangentMode GetTangentModeForPoint( int pointIndex )
	{
		CheckPointIndex( pointIndex );
		return _pointTangentModes[pointIndex];
	}

	public void SetTangentModeForPoint( int pointIndex, SplinePointTangentMode tangentMode )
	{
		CheckPointIndex( pointIndex );
		_pointTangentModes[pointIndex] = tangentMode;
		RecalculateTangentsForPointAndAdjacentPoints( pointIndex );
		RequiresDistanceResample();
	}

	// Can be used to get information via GetPositionAtDistance and GetTangentAtDistance etc.
	public float FindDistanceClosestToPosition( Vector3 position )
	{
		EnsureSplineIsDistanceSampled();

		var splineParamsForClosestPosition = Spline.Utils.FindSegmentAndTClosestToPosition( _splinePoints.AsReadOnly(), position );

		return _distanceSampler.GetDistanceAtSplineParams( splineParamsForClosestPosition );
	}

	public void ConvertToPolyline( ref List<Vector3> outPolyLine )
	{
		outPolyLine.Clear();

		EnsureSplineIsDistanceSampled();

		Spline.Utils.ConvertSplineToPolyLineWithCachedSampler( _splinePoints.AsReadOnly(), ref outPolyLine, _distanceSampler, 0.1f );
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
			Spline.Utils.ConvertSplineToPolyLineWithCachedSampler( _splinePoints.AsReadOnly(), ref _drawCachePolyline, _distanceSampler, 0.1f );

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
		if ( !Gizmo.Camera.GetFrustum( Gizmo.Camera.Rect, 1 ).IsInside( _distanceSampler.GetTotalBounds().Transform(WorldTransform), true ) )
		{
			return;
		}

		using ( Gizmo.Scope( "spline" ) )
		{
			float lineThickness = 2f;

			if ( _splinePoints.Count < 1 )
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
						result.Distance = FindDistanceClosestToPosition( point_on_line );
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
		}
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
			for ( var i = 0; i < _splinePoints.Count; i++ )
			{
				if ( !IsLoop || i != _splinePoints.Count - 1 )
				{
					var splinePoint = _splinePoints[i];

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
		for ( var i = 0; i < _splinePoints.Count; i++ )
		{
			if ( !IsLoop || i != _splinePoints.Count - 1 )
			{
				DrawPointGizmo( i, isHovered );
			}
		}
	}
	private void DrawPointGizmo( int pointIndex, bool isHovered )
	{
		CheckPointIndex( pointIndex );

		var splinePoint = _splinePoints[pointIndex];

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

	private void RecalculateTangentsForPointAndAdjacentPoints( int pointIndex )
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
		switch ( _pointTangentModes[index] )
		{
			case Spline.SplinePointTangentMode.Auto:
				_splinePoints[index] = Spline.Utils.CalculateSmoothTangentForPoint( _splinePoints.AsReadOnly(), index );
				break;
			case Spline.SplinePointTangentMode.Linear:
				_splinePoints[index] = Spline.Utils.CalculateLinearTangentForPoint( _splinePoints.AsReadOnly(), index );
				break;
			case Spline.SplinePointTangentMode.Split:
				break;
			case Spline.SplinePointTangentMode.Mirrored:
				_splinePoints[index] = _splinePoints[index] with { OutPositionRelative = -_splinePoints[index].InPositionRelative };
				break;
		}

		if ( IsLoop && index == 0 )
		{
			_splinePoints[_splinePoints.Count - 1] = _splinePoints[0];
			_pointRolls[_pointRolls.Count - 1] = _pointRolls[0];
			_pointScales[_pointScales.Count - 1] = _pointScales[0];
		}
	}
}
