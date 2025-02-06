using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SplineMath;

/// <summary>
/// Collection of curves in 3D space.
/// Shape and behavior of the curves are controled through points <see cref="Spline.Point"/>, each with customizable handles, roll, scale, and up vectors.
/// Two consecutive points define a segment/curve of the spline.
/// <br /><br />
/// By adjusting the handles both smooth and sharp corners can be created.
/// The spline can also be turned into a loop, combined with linear tangents this can be used to create polygons.
/// Splines can also be used used for animations, camera movements, marking areas, or procedural geometry generation.
/// </summary>
public class Spline
{
	/// <summary>
	/// Point that defines part of the spline.
	/// Two consecutive points define a segment of the spline.
	/// The <see cref="Position" />,  <see cref="In" />/<see cref="Out" /> Handles and <see cref="Mode"></see> / properties are used to define the shape of the spline.
	/// <code>
	///                  P1 (Position)                         
	///       P1 (In)           ▼           P1 (Out)                      
	///               o──────═══X═══──────o                    
	///                  ───/       \───                      
	///               ──/               \──                   
	///             -/                     \-                  
	///            /                         \                 
	///           |                           |
	///       P0  X                           X  P2
	/// </code>
	/// </summary>
	[JsonConverter( typeof( SplinePointConverter ) )]
	public struct Point
	{
		/// <summary>
		/// The position of the spline point.
		/// </summary>
		public Vector3 Position;

		/// <summary>
		/// The Out Position for the curve handle.
		/// </summary>
		[Hide]
		public Vector3 Out
		{
			get => Position + OutRelative;
			set { OutRelative = value - Position; }
		}

		/// <summary>
		/// The In Position for the curve handle.
		/// </summary>
		[Hide]
		public Vector3 In
		{
			get => Position + InRelative;
			set { InRelative = value - Position; }
		}

		/// <summary>
		/// Describes how the spline should behave when entering/leaving a point.
		/// The mmode and the handles In and Out position will determine the transition between segments.
		/// </summary>
		public HandleMode Mode = HandleMode.Auto;

		/// <summary>
		/// Roll/Twist around the tangent axis.
		/// </summary>
		public float Roll = 0f;

		/// <summary>
		/// X = Scale Length, Y = Scale Width, Z = Scale Height
		/// </summary>
		public Vector3 Scale = Vector3.One;

		/// <summary>
		/// Custom up vector at a spline point, can be used to calculate tangent frames (transforms) along the spline.
		/// This allows fine grained control over the orientation of objects following the spline.
		/// </summary>
		public Vector3 Up = Vector3.Up;

		/// <summary>
		/// Position of the In handle relative to the point position.
		/// </summary>
		public Vector3 InRelative;

		/// <summary>
		/// Position of the Out handle relative to the point position.
		/// </summary>
		public Vector3 OutRelative;

		public Point()
		{

		}

		public override string ToString()
		{
			return $"Position: {Position}, In: {InRelative}, Out: {OutRelative}, Mode: {Mode}, Roll: {Roll}, Scale: {Scale}, Up: {Up}";
		}
	}

	/// <summary>
	/// Describes how the spline should behave when entering/leaving a point.
	/// </summary>
	public enum HandleMode
	{
		/// <summary>
		/// Handle positions are calculated automatically
		/// based on the location of adjacent points.
		/// </summary>
		[Icon( "auto_fix_high" )]
		Auto,
		/// <summary>
		/// Handle positions are set to zero, leading to a sharp corner.
		/// </summary>
		[Icon( "show_chart" )]
		Linear,
		/// <summary>
		/// The In and Out handles are user set, but are linked (mirrored).
		/// </summary>
		[Icon( "open_in_full" )]
		Mirrored,
		/// <summary>
		/// The In and Out handle are user set and operate independently.
		/// </summary>
		[Icon( "call_split" )]
		Split,
	}

	// private because we need to ensure the points are always in a valid state
	[JsonInclude, JsonPropertyName( "Points" )]
	private List<Point> _points = new();

	private bool _areDistancesSampled = false;

	/// <summary>
	/// Invoked everytime the spline shape or the properties of the spline change.
	/// </summary>
	public Action SplineChanged;

	private SplineUtils.SplineSampler _distanceSampler = new();

	private void RequiresDistanceResample()
	{
		_areDistancesSampled = false;
		SplineChanged?.Invoke();
	}

	private void SampleDistances()
	{
		_distanceSampler.Sample( _points.AsReadOnly() );
		_areDistancesSampled = true;
	}

	private void EnsureSplineIsDistanceSampled()
	{
		if ( _areDistancesSampled )
		{
			return;
		}
		SampleDistances();
	}

	/// <summary>
	/// Whether the spline forms a loop.
	/// </summary>
	[Property, JsonIgnore]
	public bool IsLoop
	{
		get => SplineUtils.IsLoop( _points.AsReadOnly() );
		set
		{
			var isAlreadyLoop = SplineUtils.IsLoop( _points.AsReadOnly() );
			// We emulate loops by adding an addtional point at the end which matches the first point
			// this might seem hacky at first but it makes things so much easier downstream,
			// because we can handle open splines and looped splines exactly the same when doing complex calculations
			// The fact that the last point exists will be hidden from the user in the Editor and API
			if ( value && !isAlreadyLoop )
			{
				_points.Add( _points[0] );
				RequiresDistanceResample();
			}
			else if ( !value && isAlreadyLoop )
			{
				_points.RemoveAt( _points.Count - 1 );
				RequiresDistanceResample();
			}
		}
	}

	/// <summary>
	/// Information about the spline at a specific distance.
	/// </summary>
	public struct Sample
	{
		public Vector3 Position;
		public Vector3 Tangent;
		public float Roll;
		public Vector3 Scale;
		public Vector3 Up;
		public float Distance;
	}

	/// <summary>
	/// Calculates a bunch of information about the spline at a specific distance.
	/// </summary>
	public Sample SampleAtDistance( float distance )
	{
		EnsureSplineIsDistanceSampled();
		var splineParams = _distanceSampler.CalculateSegmentParamsAtDistance( distance );
		var distanceAlongSegment = distance - _distanceSampler.GetSegmentStartDistance( splineParams.Index );
		var segmentLength = _distanceSampler.GetSegmentLength( splineParams.Index );
		var position = SplineUtils.GetPosition( _points.AsReadOnly(), splineParams );
		var tangent = SplineUtils.GetTangent( _points.AsReadOnly(), splineParams );
		var roll = MathX.Lerp( _points[splineParams.Index].Roll, _points[splineParams.Index + 1].Roll, distanceAlongSegment / segmentLength );
		var scale = Vector3.Lerp( _points[splineParams.Index].Scale, _points[splineParams.Index + 1].Scale, distanceAlongSegment / segmentLength );
		var upVector = Vector3.Lerp( _points[splineParams.Index].Up, _points[splineParams.Index + 1].Up, distanceAlongSegment / segmentLength );
		return new Sample
		{
			Position = position,
			Tangent = tangent,
			Roll = roll,
			Scale = scale,
			Up = upVector,
			Distance = distance
		};
	}

	/// <summary>
	/// Calculates a bunch of information about the spline at the position closest to the specified position.
	/// </summary>
	public Sample SampleAtClosestPosition( Vector3 position )
	{
		var distance = FindDistanceClosestToPosition( position );
		return SampleAtDistance( distance );
	}

	/// <summary>
	/// Total length of the spline.
	/// </summary>
	public float Length
	{
		get
		{
			EnsureSplineIsDistanceSampled();
			return _distanceSampler.TotalLength();
		}
	}

	/// <summary>
	/// Total bounds of the spline.
	/// </summary>
	public BBox Bounds
	{
		get
		{
			EnsureSplineIsDistanceSampled();
			return _distanceSampler.GetTotalBounds();
		}
	}

	/// <summary>
	/// Fetches how far along the spline a point is.
	/// </summary>
	public float GetDistanceAtPoint( int pointIndex )
	{
		CheckPointIndex( pointIndex );
		EnsureSplineIsDistanceSampled();

		if ( pointIndex == _points.Count - 1 )
		{
			return _distanceSampler.TotalLength();
		}
		return _distanceSampler.GetSegmentStartDistance( pointIndex );
	}

	/// <summary>
	/// Fetches the length of an individual spline segment.
	/// </summary>
	public float GetSegmentLength( int segmentIndex )
	{
		CheckSegmentIndex( segmentIndex );
		EnsureSplineIsDistanceSampled();

		return _distanceSampler.GetSegmentLength( segmentIndex );
	}

	/// <summary>
	/// Bounds of an individual spline segment.
	/// </summary>
	public BBox GetSegmentBounds( int segmentIndex )
	{
		CheckSegmentIndex( segmentIndex );
		EnsureSplineIsDistanceSampled();

		return _distanceSampler.GetSegmentBounds( segmentIndex );
	}

	/// <summary>
	/// Access the information about a spline point.
	/// </summary>
	public Point GetPoint( int pointIndex )
	{
		CheckPointIndex( pointIndex );

		return _points[pointIndex];
	}

	/// <summary>
	/// Number of points in the spline.
	/// </summary>
	public int PointCount => IsLoop ? _points.Count - 1 : _points.Count;

	/// <summary>
	/// Number of segments in the spline, a spline contains one less segment than points.
	/// </summary>
	public int SegmentCount => SplineUtils.SegmentNum( _points.AsReadOnly() );

	/// <summary>
	/// Update the information stored at a spline point.
	/// </summary>
	public void UpdatePoint( int pointIndex, Point updatedPoint )
	{
		CheckPointIndex( pointIndex );

		_points[pointIndex] = updatedPoint;

		RecalculateTangentsForPointAndAdjacentPoints( pointIndex );

		RequiresDistanceResample();
	}

	/// <summary>
	/// Adds a point at an index
	/// </summary>
	public void InsertPoint( int pointIndex, Point newPoint )
	{
		CheckInsertPointIndex( pointIndex );

		_points.Insert( pointIndex, newPoint );

		RecalculateTangentsForPointAndAdjacentPoints( pointIndex );

		RequiresDistanceResample();
	}

	/// <summary>
	/// Adds a point to the end of the spline.
	/// </summary>
	public void AddPoint( Point newPoint )
	{
		_points.Add( newPoint );
		RecalculateTangentsForPointAndAdjacentPoints( _points.Count - 1 );
		RequiresDistanceResample();
	}

	/// <summary>
	/// Adds a point at a specific distance along the spline.
	/// Returns the index of the added spline point.
	/// Tangents of the new point and adjacent points will be calculated so the spline shape remains the same.
	/// Unless inferTangentModes is set to true, in which case the tangent modes will be inferred from the adjacent points.
	/// </summary>
	public int AddPointAtDistance( float distance, bool inferTangentModes = false )
	{
		EnsureSplineIsDistanceSampled();

		var splineParams = _distanceSampler.CalculateSegmentParamsAtDistance( distance );

		var distanceParam = (distance - _distanceSampler.GetSegmentStartDistance( splineParams.Index )) / _distanceSampler.GetSegmentLength( splineParams.Index );

		var positionSplitResult = SplineUtils.SplitSegment( _points.AsReadOnly(), splineParams, distanceParam );

		// modify points before and after the split
		_points[splineParams.Index] = positionSplitResult.Left;
		_points[splineParams.Index + 1] = positionSplitResult.Right;

		var newPointIndex = splineParams.Index + 1;

		_points.Insert( newPointIndex, positionSplitResult.Mid );

		RecalculateTangentsForPointAndAdjacentPoints( newPointIndex );

		RequiresDistanceResample();

		return newPointIndex;
	}

	/// <summary>
	/// Removes the point at the specified index.
	/// </summary>
	public void RemovePoint( int pointIndex )
	{
		CheckPointIndex( pointIndex );

		_points.RemoveAt( pointIndex );

		if ( pointIndex - 1 >= 0 )
		{
			RecalculateTangentsForPoint( pointIndex - 1 );
		}

		if ( pointIndex < _points.Count )
		{
			RecalculateTangentsForPoint( pointIndex );
		}

		RequiresDistanceResample();
	}

	/// <summary>
	/// Removes all points from the spline.
	/// </summary>
	public void Clear()
	{
		_points.Clear();

		RequiresDistanceResample();
	}

	/// <summary>
	/// Can be used to get information via GetPositionAtDistance and GetTangentAtDistance etc.
	/// </summary>
	private float FindDistanceClosestToPosition( Vector3 position )
	{
		EnsureSplineIsDistanceSampled();

		var splineParamsForClosestPosition = SplineUtils.FindSegmentAndTClosestToPosition( _points.AsReadOnly(), position );

		return _distanceSampler.GetDistanceAtSplineParams( splineParamsForClosestPosition );
	}

	/// <summary>
	/// Converts the spline to a polyline, can pass in buffer as parameter to avoid reallocations.
	/// </summary>
	public void ConvertToPolyline( ref List<Vector3> outPolyLine )
	{
		outPolyLine.Clear();

		EnsureSplineIsDistanceSampled();

		SplineUtils.ConvertSplineToPolyLineWithCachedSampler( _points.AsReadOnly(), ref outPolyLine, _distanceSampler, 0.1f );
	}

	/// <summary>
	/// Converts the spline to a polyline.
	/// </summary>
	public List<Vector3> ConvertToPolyline()
	{
		var outPolyLine = new List<Vector3>();
		ConvertToPolyline( ref outPolyLine );
		return outPolyLine;
	}

	// Internal for now no need to expose this yet without, spline deformers
	internal Transform[] CalculateTangentFramesUsingUpDir( int frameCount )
	{
		Transform[] frames = new Transform[frameCount];

		float totalSplineLength = Length;

		Sample sample = SampleAtDistance( 0f );
		sample.Up = Vector3.Up;

		// Choose an initial up vector if tangent is parallel to Up
		if ( MathF.Abs( Vector3.Dot( sample.Tangent, sample.Up ) ) > 0.999f )
		{
			sample.Up = Vector3.Right;
		}

		for ( int i = 0; i < frameCount; i++ )
		{
			float t = (float)i / (frameCount - 1);
			float distance = t * totalSplineLength;

			sample = SampleAtDistance( distance );

			// Apply roll
			var newUp = Rotation.FromAxis( sample.Tangent, sample.Roll ) * sample.Up;

			Rotation rotation = Rotation.LookAt( sample.Tangent, newUp );

			frames[i] = new Transform( sample.Position, rotation, sample.Scale );
		}

		return frames;
	}

	// Internal for now no need to expose this yet without spline deformers
	internal Transform[] CalculateRotationMinimizingTangentFrames( int frameCount )
	{
		Transform[] frames = new Transform[frameCount];

		float totalSplineLength = Length;

		// Initialize the up vector
		Sample previousSample = SampleAtDistance( 0f );
		Vector3 up = Vector3.Up;

		// Choose an initial up vector if tangent is parallel to Up
		if ( MathF.Abs( Vector3.Dot( previousSample.Tangent, up ) ) > 0.999f )
		{
			up = Vector3.Right;
		}

		up = Rotation.FromAxis( previousSample.Tangent, previousSample.Roll ) * up;

		frames[0] = new Transform( previousSample.Position, Rotation.LookAt( previousSample.Tangent, up ), previousSample.Scale );

		for ( int i = 1; i < frameCount; i++ )
		{
			float t = (float)i / (frameCount - 1);
			float distance = t * totalSplineLength;

			Sample sample = SampleAtDistance( distance );

			// Parallel transport the up vector
			up = GetRotationMinimizingNormal( previousSample.Position, previousSample.Tangent, up, sample.Position, sample.Tangent );

			// Apply roll
			float deltaRoll = sample.Roll - previousSample.Roll;
			up = Rotation.FromAxis( sample.Tangent, deltaRoll ) * up;

			Rotation rotation = Rotation.LookAt( sample.Tangent, up );

			frames[i] = new Transform( sample.Position, rotation, sample.Scale );

			previousSample = sample;
		}

		// Correct up vectors for looped splines
		if ( IsLoop && frames.Length > 1 )
		{
			Vector3 startUp = frames[0].Rotation.Up;
			Vector3 endUp = frames[^1].Rotation.Up;

			float theta = MathF.Acos( Vector3.Dot( startUp, endUp ) ) / (frames.Length - 1);
			if ( Vector3.Dot( frames[0].Rotation.Forward, Vector3.Cross( startUp, endUp ) ) > 0 )
			{
				theta = -theta;
			}

			for ( int i = 0; i < frames.Length; i++ )
			{
				Rotation R = Rotation.FromAxis( frames[i].Rotation.Forward, (theta * i).RadianToDegree() );
				Vector3 correctedUp = R * frames[i].Rotation.Up;
				frames[i] = new Transform( frames[i].Position, Rotation.LookAt( frames[i].Rotation.Forward, correctedUp ), frames[i].Scale );
			}
		}

		return frames;
	}

	private static Vector3 GetRotationMinimizingNormal( Vector3 posA, Vector3 tangentA, Vector3 normalA, Vector3 posB, Vector3 tangentB )
	{
		// Source: https://www.microsoft.com/en-us/research/wp-content/uploads/2016/12/Computation-of-rotation-minimizing-frames.pdf
		Vector3 v1 = posB - posA;
		float v1DotV1Half = Vector3.Dot( v1, v1 ) / 2f;
		float r1 = Vector3.Dot( v1, normalA ) / v1DotV1Half;
		float r2 = Vector3.Dot( v1, tangentA ) / v1DotV1Half;
		Vector3 nL = normalA - r1 * v1;
		Vector3 tL = tangentA - r2 * v1;
		Vector3 v2 = tangentB - tL;
		float r3 = Vector3.Dot( v2, nL ) / Vector3.Dot( v2, v2 );
		return (nL - 2f * r3 * v2).Normal;
	}

	private void CheckPointIndex( int pointIndex )
	{
		if ( pointIndex < 0 || pointIndex >= _points.Count || IsLoop && pointIndex == _points.Count - 1 )
		{
			throw new ArgumentOutOfRangeException( nameof( pointIndex ), "Spline point index out of range." );
		}
	}

	// Edge case: pointIndex > _splinePoints.Count
	private void CheckInsertPointIndex( int pointIndex )
	{
		if ( pointIndex < 0 || pointIndex > _points.Count )
		{
			throw new ArgumentOutOfRangeException( nameof( pointIndex ), "Spline point index out of range." );
		}
	}

	private void CheckSegmentIndex( int segmentIndex )
	{
		if ( segmentIndex < 0 || segmentIndex >= SplineUtils.SegmentNum( _points.AsReadOnly() ) )
		{
			throw new ArgumentOutOfRangeException( nameof( segmentIndex ), "Spline segment index out of range." );
		}
	}

	private void RecalculateTangentsForPointAndAdjacentPoints( int pointIndex )
	{
		RecalculateTangentsForPoint( pointIndex );
		if ( pointIndex > 0 )
		{
			RecalculateTangentsForPoint( pointIndex - 1 );
		}

		if ( pointIndex < _points.Count - 1 )
		{
			RecalculateTangentsForPoint( pointIndex + 1 );
		}

		if ( IsLoop )
		{
			if ( pointIndex == 0 )
			{
				RecalculateTangentsForPoint( _points.Count - 2 );
			}

			if ( pointIndex == _points.Count - 2 )
			{
				RecalculateTangentsForPoint( 0 );
			}
		}
	}

	private void RecalculateTangentsForPoint( int index )
	{
		if ( IsLoop && index == _points.Count - 1 )
		{
			index = 0;
		}
		switch ( _points[index].Mode )
		{
			case HandleMode.Auto:
				_points[index] = SplineUtils.CalculateSmoothTangentForPoint( _points.AsReadOnly(), index );
				break;
			case HandleMode.Linear:
				_points[index] = SplineUtils.CalculateLinearTangentForPoint( _points.AsReadOnly(), index );
				break;
			case HandleMode.Split:
				break;
			case HandleMode.Mirrored:
				_points[index] = _points[index] with { OutRelative = -_points[index].InRelative };
				break;
		}

		if ( IsLoop && index == 0 )
		{
			_points[_points.Count - 1] = _points[0];
		}
	}
}


// Immutable and stateless spline calculations and utilities.
// Will be used by higher level abstractions such as the SplineComponent
// Note: We probably wont want to expose this until the interface is stable
// and we know exactly which of these functions are actually needed/desired
internal static class SplineUtils
{
	public struct SegmentParams
	{
		public int Index;

		public float T;
	}

	public static Vector3 GetPosition( ReadOnlyCollection<Spline.Point> spline, SegmentParams segmentParams )
	{
		CheckSegmentParams( spline, segmentParams );

		// Calculate value for cubic Bernstein polynominal
		// B(t) = (1 - t)^3 * P0 + 3 * (1 - t)^2 * t * P1 + 3 * (1 - t)^2 * t^2 * P2 + t^3 * P3
		float tSquare = segmentParams.T * segmentParams.T;
		float tCubic = tSquare * segmentParams.T;
		float oneMinusT = 1 - segmentParams.T;
		float oneMinusTSquare = oneMinusT * oneMinusT;
		float oneMinusTCubic = oneMinusTSquare * oneMinusT;

		float w0 = oneMinusTCubic; // -t^3 + 3t^2 - 3t + 1
		float w1 = 3 * oneMinusTSquare * segmentParams.T; // 3t^3 - 6t^2 + 3t
		float w2 = 3 * oneMinusT * tSquare; // -3t^3 + 3t^2
		float w3 = tCubic; // t^3

		Vector3 weightedP0 = w0 * P0( spline, segmentParams.Index );
		Vector3 weightedP1 = w1 * P1( spline, segmentParams.Index );
		Vector3 weightedP2 = w2 * P2( spline, segmentParams.Index );
		Vector3 weightedP3 = w3 * P3( spline, segmentParams.Index );

		return weightedP0 + weightedP1 + weightedP2 + weightedP3;
	}

	public static Vector3 GetTangent( ReadOnlyCollection<Spline.Point> spline, SegmentParams segmentParams )
	{
		CheckSegmentParams( spline, segmentParams );
		return GetDerivative( spline, segmentParams ).Normal;
	}

	public static Vector3 GetTangent2D( ReadOnlyCollection<Spline.Point> spline, SegmentParams segmentParams )
	{
		CheckSegmentParams( spline, segmentParams );
		return GetTangent( spline, segmentParams ).WithZ( 0 ).Normal;
	}

	public static float GetCurvature( ReadOnlyCollection<Spline.Point> spline, SegmentParams segmentParams )
	{
		CheckSegmentParams( spline, segmentParams );

		var velocity = GetDerivative( spline, segmentParams );
		var velocitySize = velocity.Length;

		var acceleration = GetSecondDerivative( spline, segmentParams );

		var curvatureAxis = Vector3.Cross( velocity, acceleration ) / MathF.Pow( velocitySize, 3 );

		return curvatureAxis.Length;
	}

	public static Vector3 GetCurvatureAxis( ReadOnlyCollection<Spline.Point> spline, SegmentParams segmentParams )
	{
		CheckSegmentParams( spline, segmentParams );
		var velocity = GetDerivative( spline, segmentParams );
		var acceleration = GetSecondDerivative( spline, segmentParams );

		return Vector3.Cross( velocity, acceleration ).Normal;
	}

	public static int SegmentNum( ReadOnlyCollection<Spline.Point> spline )
	{
		return spline.Count - 1;
	}

	private static void DivideSegmentRecursive(
		ReadOnlyCollection<Spline.Point> spline,
		int segmentIndex,
		SplineSampler sampler,
		float startDistance,
		float endDistance,
		float maxSquareError,
		SortedDictionary<float, Vector3> outPoints )
	{
		var dist = endDistance - startDistance;
		if ( dist <= 0 )
		{
			return;
		}

		var middleDistance = startDistance + dist * 0.5f;

		var startT = sampler.CalculateSegmentTAtDistance( segmentIndex, startDistance ).Value;
		var middleT = sampler.CalculateSegmentTAtDistance( segmentIndex, middleDistance ).Value;
		var endT = sampler.CalculateSegmentTAtDistance( segmentIndex, endDistance ).Value;

		var startPosition = GetPosition( spline, new SegmentParams { Index = segmentIndex, T = startT } );
		var middlePosition = GetPosition( spline, new SegmentParams { Index = segmentIndex, T = middleT } );
		var endPosition = GetPosition( spline, new SegmentParams { Index = segmentIndex, T = endT } );

		var startEndLine = new Line( startPosition, endPosition );
		if ( startEndLine.SqrDistance( middlePosition ) > maxSquareError )
		{
			DivideSegmentRecursive( spline, segmentIndex, sampler, startDistance, middleDistance, maxSquareError, outPoints );
			DivideSegmentRecursive( spline, segmentIndex, sampler, middleDistance, endDistance, maxSquareError, outPoints );
		}
		else
		{
			outPoints.Add( middleDistance, middlePosition );
		}
	}

	public static void ConvertSplineToPolyLine(
		ReadOnlyCollection<Spline.Point> spline,
		ref List<Vector3> outPoints,
		float maxSquareError = 0.1f )
	{
		if ( spline.Count == 0 )
		{
			return;
		}
		var sampler = new SplineSampler();
		sampler.Sample( spline );
		ConvertSplineToPolyLineWithCachedSampler( spline, ref outPoints, sampler, maxSquareError );
	}

	public static void ConvertSplineToPolyLineWithCachedSampler(
		ReadOnlyCollection<Spline.Point> spline,
		ref List<Vector3> outPoints,
		SplineSampler sampler,
		float maxSquareError = 0.1f )
	{
		outPoints.Clear();

		if ( spline.Count == 0 )
		{
			return;
		}

		var numSegments = SegmentNum( spline );
		const int averagePointsPerSegmentEstimate = 16;
		outPoints.Capacity = Math.Max( outPoints.Capacity, numSegments * averagePointsPerSegmentEstimate );

		var sortedPoints = new SortedDictionary<float, Vector3>();

		for ( int segmentIndex = 0; segmentIndex < numSegments; ++segmentIndex )
		{
			var startDist = sampler.GetSegmentStartDistance( segmentIndex );
			var stopDist = sampler.GetSegmentStartDistance( segmentIndex + 1 );

			sortedPoints.TryAdd( startDist, spline[segmentIndex].Position );
			sortedPoints.TryAdd( stopDist, spline[segmentIndex + 1].Position );

			DivideSegmentRecursive( spline, segmentIndex, sampler, startDist, stopDist, maxSquareError, sortedPoints );
		}

		foreach ( var point in sortedPoints )
		{
			outPoints.Add( point.Value );
		}
	}

	private static void CheckSegmentIndex( ReadOnlyCollection<Spline.Point> spline, int index )
	{
		if ( index < 0 || index > SegmentNum( spline ) - 1 )
		{
			throw new ArgumentOutOfRangeException( nameof( index ), "Index is out of range." );
		}
	}

	private static void CheckSegmentParams( ReadOnlyCollection<Spline.Point> spline, SegmentParams segmentParams )
	{
		CheckSegmentIndex( spline, segmentParams.Index );
		if ( segmentParams.T < 0.0f || segmentParams.T > 1.0f )
		{
			throw new ArgumentOutOfRangeException( nameof( segmentParams.T ), "T must be in the range [0.0, 1.0]." );
		}
	}

	private static Vector3 GetDerivative( ReadOnlyCollection<Spline.Point> spline, SegmentParams segmentParams )
	{
		CheckSegmentParams( spline, segmentParams );

		// Calculate the derivative of the cubic Bernstein polynominal
		// B'(t) = 3 * (1 - t) ^2 * (P1 - P0) + 6 * (1 - t) * t * (P2 - P1) + 3 * t^2 * (P3 - P2)
		float t2 = segmentParams.T * segmentParams.T;

		float w0 = -3 * t2 + 6 * segmentParams.T - 3;
		float w1 = 9 * t2 - 12 * segmentParams.T + 3;
		float w2 = -9 * t2 + 6 * segmentParams.T;
		float w3 = 3 * t2;

		Vector3 weightedP0 = w0 * P0( spline, segmentParams.Index );
		Vector3 weightedP1 = w1 * P1( spline, segmentParams.Index );
		Vector3 weightedP2 = w2 * P2( spline, segmentParams.Index );
		Vector3 weightedP3 = w3 * P3( spline, segmentParams.Index );

		return weightedP0 + weightedP1 + weightedP2 + weightedP3;
	}

	private static Vector3 GetSecondDerivative( ReadOnlyCollection<Spline.Point> spline,
		SegmentParams segmentParams )
	{
		CheckSegmentParams( spline, segmentParams );

		// Calculate the second derivative of the cubic Bernstein polynominal
		// B''(t) = 6 * (1 - t) * (P2 - 2 * P1 + P0) + 6 * t * (P3 - 2 * P2 + P1)
		float w0 = -6 * segmentParams.T + 6;
		float w1 = 18 * segmentParams.T - 12;
		float w2 = -18 * segmentParams.T + 6;
		float w3 = 6 * segmentParams.T;

		Vector3 weightedP0 = w0 * P0( spline, segmentParams.Index );
		Vector3 weightedP1 = w1 * P1( spline, segmentParams.Index );
		Vector3 weightedP2 = w2 * P2( spline, segmentParams.Index );
		Vector3 weightedP3 = w3 * P3( spline, segmentParams.Index );

		return weightedP0 + weightedP1 + weightedP2 + weightedP3;
	}

	private static Vector3 GetThirdDerivative( ReadOnlyCollection<Spline.Point> spline, int segmentIndex )
	{
		CheckSegmentIndex( spline, segmentIndex );

		// Calculate the third derivative of the cubic Bernstein polynominal
		// B'''(t) = - 6 * P0 + 18 * P1 - 18 * P2 + 6 * P3
		const float w0 = -6;
		const float w1 = 18;
		const float w2 = -18;
		const float w3 = 6;

		Vector3 weightedP0 = w0 * P0( spline, segmentIndex );
		Vector3 weightedP1 = w1 * P1( spline, segmentIndex );
		Vector3 weightedP2 = w2 * P2( spline, segmentIndex );
		Vector3 weightedP3 = w3 * P3( spline, segmentIndex );

		return weightedP0 + weightedP1 + weightedP2 + weightedP3;
	}

	// In 3D we can only calculate the Root for each axis separately
	// We accomplish this by projecting the spline segment onto the axis and then solving the 1D cubic equation
	private static List<float> CalculateRootsForAxis( ReadOnlyCollection<Spline.Point> spline, int segmentIndex, int axis )
	{
		CheckSegmentIndex( spline, segmentIndex );

		var p0Component = P0( spline, segmentIndex )[axis];
		var p1Component = P1( spline, segmentIndex )[axis];
		var p2Component = P2( spline, segmentIndex )[axis];
		var p3Component = P3( spline, segmentIndex )[axis];

		var a = -p0Component + 3 * (p1Component - p2Component) + p3Component;
		var b = 3 * (p0Component - 2 * p1Component + p2Component);
		var c = 3 * (-p0Component + p1Component);
		var d = p0Component;

		var roots = MathY.SolveCubic( a, b, c, d );
		roots.RemoveAll( root => root < 0.0f || root > 1.0f );

		return roots;
	}

	// In 3D we can only calculate the extrema for each axis separately
	// We accmplish this by projecting the spline segment onto the axis and then solving the 1D quadratic equation of the derivative
	private static List<float> CalculateLocalExtremaForAxis( ReadOnlyCollection<Spline.Point> spline, int segmentIndex,
		int axis )
	{
		CheckSegmentIndex( spline, segmentIndex );

		var p0Component = P0( spline, segmentIndex )[axis];
		var p1Component = P1( spline, segmentIndex )[axis];
		var p2Component = P2( spline, segmentIndex )[axis];
		var p3Component = P3( spline, segmentIndex )[axis];

		// Solve derivative for extrema
		var a = 3 * (-p0Component + 3 * (p1Component - p2Component) + p3Component);
		var b = 6 * (p0Component - 2 * p1Component + p2Component);
		var c = 3 * (-p0Component + p1Component);

		var roots = MathY.SolveQuadratic( a, b, c );
		roots.RemoveAll( root => root < 0.0f || root > 1.0f );

		return roots;
	}

	// For the inflections we project the spline segment onto the axis and then solve the 1D cubic equation of the second derivative
	public static List<float> CalculateInflections2D( ReadOnlyCollection<Spline.Point> spline, int segmentIndex )
	{
		CheckSegmentIndex( spline, segmentIndex );

		var alignmentInfo = CalculateAlignmentInfoForPoints( P0( spline, segmentIndex ), P3( spline, segmentIndex ) );
		var alignedSplineSegment = AlignSegment( spline, segmentIndex, alignmentInfo );

		var x1 = P1( alignedSplineSegment.AsReadOnly(), 0 ).x;
		var x2 = P2( alignedSplineSegment.AsReadOnly(), 0 ).x;
		var x3 = P3( alignedSplineSegment.AsReadOnly(), 0 ).x;

		var y1 = P1( alignedSplineSegment.AsReadOnly(), 0 ).y;
		var y2 = P2( alignedSplineSegment.AsReadOnly(), 0 ).y;

		var p = x2 * y1;
		var q = x3 * y1;
		var r = x1 * y2;
		var s = x3 * y2;

		var a = (-3 * p + 2 * q + 3 * r - s) * 18;
		var b = (3 * p - q - 3 * r) * 18;
		var c = (r - p) * 18;

		var roots = MathY.SolveQuadratic( a, b, c );
		roots.RemoveAll( root => root < 0.0f || root > 1.0f );

		return roots;
	}

	// Helper function used to add the extrema to the bounding box
	private static void AddExtremaToBox( ReadOnlyCollection<Spline.Point> spline, int segmentIndex, ref BBox inBox )
	{
		for ( int axis = 0; axis < 3; ++axis )
		{
			var extrema = CalculateLocalExtremaForAxis( spline, segmentIndex, axis );
			foreach ( var extremaT in extrema )
			{
				inBox = inBox.AddPoint( GetPosition( spline,
					new SegmentParams { Index = segmentIndex, T = extremaT } ) );
			}
		}
	}

	// The bounding box of a spline segment is defined by the start and end points of the segment and local extrema.
	private static BBox CalculateBoundingBoxForSegment( ReadOnlyCollection<Spline.Point> spline, int segmentIndex )
	{
		BBox result = BBox.FromPositionAndSize( GetPosition( spline, new SegmentParams { Index = segmentIndex, T = 0f } ) );

		result = result.AddPoint( GetPosition( spline, new SegmentParams { Index = segmentIndex, T = 1f } ) );
		AddExtremaToBox( spline, segmentIndex, ref result );

		return result;
	}


	public static bool IsLoop( ReadOnlyCollection<Spline.Point> spline )
	{
		return spline.Count >= 3 && spline[0].Position.AlmostEqual( spline[spline.Count - 1].Position, 0.01f );
	}

	// Calculates the Bounding box of the entire spline, by combining the bounding boxes of each segment.
	public static BBox CalculateBoundingBox( ReadOnlyCollection<Spline.Point> spline )
	{
		var inputSegmentNum = SegmentNum( spline );

		if ( inputSegmentNum == 0 )
		{
			return BBox.FromPositionAndSize( Vector3.Zero );
		}

		BBox result = CalculateBoundingBoxForSegment( spline, 0 );

		for ( int segmentIndex = 1; segmentIndex < inputSegmentNum; ++segmentIndex )
		{
			result = result.AddBBox( CalculateBoundingBoxForSegment( spline, segmentIndex ) );
		}

		return result;
	}

	private static BBox CalculateAlignedBoundingBoxForSegment( ReadOnlyCollection<Spline.Point> spline, int segmentIndex,
		SegmentAlignmentInfo alignmentInfo )
	{
		var alignedSplineSegment = AlignSegment( spline, segmentIndex, alignmentInfo );

		return CalculateBoundingBoxForSegment( alignedSplineSegment.AsReadOnly(), 0 );
	}

	private static OrientedBoundingBox ReverseAlignmentForBoundingBox( BBox alignedBoundingBox,
		SegmentAlignmentInfo alignmentInfo )
	{
		Transform boxTransform = Transform.Zero;
		boxTransform.Position =
			alignmentInfo.RotationInverse * alignedBoundingBox.Center + alignmentInfo.TranslationInverse;
		boxTransform.Rotation = alignmentInfo.RotationInverse;

		return new OrientedBoundingBox { Transform = boxTransform, Extents = alignedBoundingBox.Extents };
	}

	public static OrientedBoundingBox CalculateMinOrientedBoundingBox( ReadOnlyCollection<Spline.Point> spline )
	{
		var inputSegmentNum = SegmentNum( spline );

		if ( inputSegmentNum == 0 )
		{
			return new OrientedBoundingBox
			{
				Transform = Transform.Zero,
				Extents = new Vector3( 0, 0, 0 )
			};
		}

		OrientedBoundingBox currentMinBBox = new OrientedBoundingBox
		{
			Transform = Transform.Zero,
			Extents = new Vector3( float.MaxValue, float.MaxValue, float.MaxValue )
		};
		for ( int alignmentSegmentCandidateIndex = 0;
			 alignmentSegmentCandidateIndex < inputSegmentNum;
			 ++alignmentSegmentCandidateIndex )
		{
			var alignmentInfo = CalculateAlignmentInfoForPoints( P0( spline, alignmentSegmentCandidateIndex ),
				P3( spline, alignmentSegmentCandidateIndex ) );

			BBox alignedBoundingBox = CalculateAlignedBoundingBoxForSegment( spline, 0, alignmentInfo );
			for ( int segmentIndex = 1; segmentIndex < inputSegmentNum; ++segmentIndex )
			{
				alignedBoundingBox =
					alignedBoundingBox.AddBBox(
						CalculateAlignedBoundingBoxForSegment( spline, segmentIndex, alignmentInfo ) );
			}

			if ( alignedBoundingBox.Extents.LengthSquared < currentMinBBox.Extents.LengthSquared )
			{
				currentMinBBox = ReverseAlignmentForBoundingBox( alignedBoundingBox, alignmentInfo );
			}
		}

		return currentMinBBox;
	}

	private static double CalculateArea2DForSegment( ReadOnlyCollection<Spline.Point> spline, int segmentIndex )
	{
		// http://ich.deanmcnamee.com/graphics/2016/03/30/CurveArea.html
		var point0 = P0( spline, segmentIndex );
		var point1 = P1( spline, segmentIndex );
		var point2 = P2( spline, segmentIndex );
		var point3 = P3( spline, segmentIndex );

		return 3.0 *
			   ((point3.y - point0.y) * (point1.x + point2.x) -
				(point3.x - point0.x) * (point1.y + point2.y) +
				point1.y * (point0.x - point2.x) - point1.x * (point0.y - point2.y) +
				point3.y * (point2.x + point0.x / 3.0) - point3.x * (point2.y + point0.y / 3.0)) /
			   20.0;
	}

	public static double CalculateArea2D( ReadOnlyCollection<Spline.Point> spline )
	{
		if ( !IsLoop( spline ) )
		{
			throw new ArgumentException( nameof( spline ), "The spline must be a loop (first and last points must be almost equal)." );
		}

		var splineSegmentNum = SegmentNum( spline );
		double result = 0.0;

		for ( int segmentIndex = 0; segmentIndex < splineSegmentNum; ++segmentIndex )
		{
			result += CalculateArea2DForSegment( spline, segmentIndex );
		}

		return result;
	}

	public static Vector3 P0( ReadOnlyCollection<Spline.Point> spline, int segmentIndex )
	{
		CheckSegmentIndex( spline, segmentIndex );
		return spline[segmentIndex].Position;
	}

	public static Vector3 P1( ReadOnlyCollection<Spline.Point> spline, int segmentIndex )
	{
		CheckSegmentIndex( spline, segmentIndex );
		return spline[segmentIndex].Out;
	}

	public static Vector3 P2( ReadOnlyCollection<Spline.Point> spline, int segmentIndex )
	{
		CheckSegmentIndex( spline, segmentIndex );
		return spline[segmentIndex + 1].In;
	}

	public static Vector3 P3( ReadOnlyCollection<Spline.Point> spline, int segmentIndex )
	{
		CheckSegmentIndex( spline, segmentIndex );
		return spline[segmentIndex + 1].Position;
	}

	public struct SplitSegmentResult
	{
		public Spline.Point Left;
		public Spline.Point Mid;
		public Spline.Point Right;
	}

	// https://www.youtube.com/watch?v=lPJo1jayLdc
	public static SplitSegmentResult SplitSegment( ReadOnlyCollection<Spline.Point> spline, SegmentParams segmentParams, float distanceParam, bool inferTangentMode = false )
	{
		CheckSegmentParams( spline, segmentParams );

		var point0 = P0( spline, segmentParams.Index );
		var point1 = P1( spline, segmentParams.Index );
		var point2 = P2( spline, segmentParams.Index );
		var point3 = P3( spline, segmentParams.Index );

		var leftOut = point0 + (point1 - point0) * segmentParams.T;
		var b = point1 + (point2 - point1) * segmentParams.T;
		var rightIn = point2 + (point3 - point2) * segmentParams.T;
		var midIn = leftOut + (b - leftOut) * segmentParams.T;
		var midOut = b + (rightIn - b) * segmentParams.T;
		var midPoint = midIn + (midOut - midIn) * segmentParams.T;

		var result = new SplitSegmentResult
		{
			Left = new Spline.Point
			{
				Position = point0,
				InRelative = spline[segmentParams.Index].InRelative,
				OutRelative = leftOut - point0,
				Mode = inferTangentMode ? spline[segmentParams.Index].Mode : Spline.HandleMode.Split,
				Roll = spline[segmentParams.Index].Roll,
				Scale = spline[segmentParams.Index].Scale,
				Up = spline[segmentParams.Index].Up
			},
			Mid = new Spline.Point
			{
				Position = midPoint,
				InRelative = midIn - midPoint,
				OutRelative = midOut - midPoint,
				Mode = inferTangentMode ? InferTangentModeForSplitPoint( spline, segmentParams ) : Spline.HandleMode.Split,
				Roll = MathX.Lerp( spline[segmentParams.Index].Roll, spline[segmentParams.Index + 1].Roll, distanceParam ),
				Scale = Vector2.Lerp( spline[segmentParams.Index].Scale, spline[segmentParams.Index + 1].Scale, distanceParam ),
				Up = Vector3.Lerp( spline[segmentParams.Index].Up, spline[segmentParams.Index + 1].Up, distanceParam )

			},
			Right = new Spline.Point
			{
				Position = point3,
				InRelative = rightIn - point3,
				OutRelative = spline[segmentParams.Index + 1].OutRelative,
				Mode = inferTangentMode ? spline[segmentParams.Index + 1].Mode : Spline.HandleMode.Split,
				Roll = spline[segmentParams.Index + 1].Roll,
				Scale = spline[segmentParams.Index + 1].Scale,
				Up = spline[segmentParams.Index + 1].Up
			}
		};

		return result;
	}

	private static Spline.HandleMode InferTangentModeForSplitPoint( ReadOnlyCollection<Spline.Point> spline, SegmentParams segmentParams )
	{
		// If the tangent modes are the same on both sides we just assume the new points should have the same tangent mode
		if ( spline[segmentParams.Index].Mode == spline[segmentParams.Index + 1].Mode )
		{
			return spline[segmentParams.Index].Mode;
		}

		// if one of them uses auto asume the new points should use auto
		if ( spline[segmentParams.Index].Mode == Spline.HandleMode.Auto ||
			 spline[segmentParams.Index + 1].Mode == Spline.HandleMode.Auto )
		{
			return Spline.HandleMode.Auto;
		}

		// If one of them uses linear assume the new points should use linear
		if ( spline[segmentParams.Index].Mode == Spline.HandleMode.Linear ||
			 spline[segmentParams.Index + 1].Mode == Spline.HandleMode.Linear )
		{
			return Spline.HandleMode.Linear;
		}

		// Otherwise we default to custom
		return Spline.HandleMode.Split;
	}


	// https://github.com/erich666/GraphicsGems/blob/master/gems/FitCurves.c
	public static SegmentParams FindSegmentAndTClosestToPosition(
		ReadOnlyCollection<Spline.Point> spline,
		Vector3 queryPosition )
	{
		const int pointsChecked = 3;
		const int iterationNum = 3;

		SegmentParams result = new SegmentParams();
		float closestDistanceSq = float.MaxValue;

		Span<float> initialTs = new float[pointsChecked] { 0.0f, 0.5f, 1.0f };

		for ( int segmentIndex = 0; segmentIndex < SegmentNum( spline ); ++segmentIndex )
		{
			for ( int i = 0; i < pointsChecked; ++i )
			{
				var (t, distanceSq) = NewtonRaphsonRootFind( spline, segmentIndex, queryPosition, initialTs[i], iterationNum );

				if ( distanceSq < closestDistanceSq )
				{
					closestDistanceSq = distanceSq;
					result.Index = segmentIndex;
					result.T = t;
				}
			}
		}

		return result;
	}

	private static (float, float) NewtonRaphsonRootFind(
		ReadOnlyCollection<Spline.Point> spline,
		int segmentIndex,
		Vector3 queryPosition,
		float currentCandidate,
		int iterationNum )
	{
		Vector3 delta = Vector3.Zero;
		for ( int iteration = 0; iteration < iterationNum; ++iteration )
		{
			Vector3 position = GetPosition( spline, new SegmentParams { Index = segmentIndex, T = currentCandidate } );
			Vector3 firstDerivative = GetDerivative( spline, new SegmentParams { Index = segmentIndex, T = currentCandidate } );
			Vector3 secondDerivative = GetSecondDerivative( spline, new SegmentParams { Index = segmentIndex, T = currentCandidate } );
			delta = position - queryPosition;

			float numerator = Vector3.Dot( delta, firstDerivative );
			float denominator = firstDerivative.LengthSquared + Vector3.Dot( delta, secondDerivative );

			if ( denominator == 0.0f )
				break;

			float movedCandidate = currentCandidate - numerator / denominator;
			currentCandidate = Math.Clamp( movedCandidate, 0.0f, 1.0f );
		}
		return (currentCandidate, delta.LengthSquared);
	}


	// Technically we could use a single Transform instead to store the same information.
	// But we dont need scale and the struct below makes the intent clearer
	// In addition we avoid a bunch of matrix multiplications at the cost of a bit extra memory.
	private struct SegmentAlignmentInfo
	{
		public Vector3 Translation;
		public Vector3 TranslationInverse;
		public Rotation Rotation;
		public Rotation RotationInverse;
	}

	// Calculate the translation and rotation needed to align the segment to the x-axis.
	// Specfically means that the tangent at the start of the segment is parallel to the x-axis and starts at the origin.
	private static SegmentAlignmentInfo CalculateAlignmentInfoForPoints( Vector3 axisOrigin,
		Vector3 pointOnPositiveAxis )
	{
		var alignmentTranslationInverse = axisOrigin;
		var alignmentTranslation = -alignmentTranslationInverse;

		var translatedLastPoint = pointOnPositiveAxis + alignmentTranslation;
		var alignmentRotationInverse = Rotation.From(
			MathF.Atan2( translatedLastPoint.z,
				MathF.Sqrt( translatedLastPoint.x * translatedLastPoint.x +
							translatedLastPoint.y * translatedLastPoint.y ) ) * (180.0f / MathF.PI),
			MathF.Atan2( translatedLastPoint.y, translatedLastPoint.x ) * (180.0f / MathF.PI), 0.0f );
		var alignmentRotation = alignmentRotationInverse.Inverse;

		return new SegmentAlignmentInfo
		{
			Translation = alignmentTranslation,
			TranslationInverse = alignmentTranslationInverse,
			Rotation = alignmentRotation,
			RotationInverse = alignmentRotationInverse
		};
	}

	// Align the segment to the x-axis using the translation and rotation calculated by CalculateAlignmentInfoForPoints.
	private static Spline.Point[] AlignSegment( ReadOnlyCollection<Spline.Point> spline, int segmentIndex,
		SegmentAlignmentInfo alignmentInfo )
	{
		var alignedSegment = new Spline.Point[2];
		alignedSegment[0].Position = alignmentInfo.Rotation * (spline[segmentIndex].Position + alignmentInfo.Translation);
		alignedSegment[0].InRelative = Vector3.Zero;
		alignedSegment[0].OutRelative = alignmentInfo.Rotation * spline[segmentIndex].OutRelative;

		alignedSegment[1].Position =
			alignmentInfo.Rotation * (spline[segmentIndex + 1].Position + alignmentInfo.Translation);
		alignedSegment[1].InRelative = alignmentInfo.Rotation * spline[segmentIndex + 1].InRelative;
		alignedSegment[1].OutRelative = Vector3.Zero;

		return alignedSegment;
	}

	// Calculates a linear tangent for a point on the spline and returns a new spline point with the tangent.
	public static Spline.Point CalculateLinearTangentForPoint( ReadOnlyCollection<Spline.Point> spline, int pointIndex )
	{
		return spline[pointIndex] with { InRelative = Vector3.Zero, OutRelative = Vector3.Zero };
	}

	// Calculate a smooth tangent for a point on the spline and return new spline point with the tangent.
	public static Spline.Point CalculateSmoothTangentForPoint( ReadOnlyCollection<Spline.Point> spline, int pointIndex )
	{
		if ( spline.Count < 2 )
		{
			return spline[pointIndex];
		}

		// Unfortunatly we need to check for the loop case as we duplicate the first and last point for loops
		// so this "virtual" last point needs to be skipped.
		var isLoop = IsLoop( spline );
		int prevIndex, nextIndex;

		if ( pointIndex == 0 )
		{
			prevIndex = isLoop ? spline.Count - 2 : 0;
			nextIndex = 1;
		}
		else if ( pointIndex == spline.Count - 1 )
		{
			prevIndex = spline.Count - 2;
			nextIndex = isLoop ? 1 : spline.Count - 1;
		}
		else
		{
			prevIndex = pointIndex - 1;
			nextIndex = pointIndex + 1;
		}

		Vector3 prevPoint = spline[prevIndex].Position;
		Vector3 nextPoint = spline[nextIndex].Position;
		Vector3 currentPoint = spline[pointIndex].Position;
		Vector3 tangent = CalculateSmoothTangent( prevPoint, currentPoint, nextPoint );

		return spline[pointIndex] with { InRelative = -tangent, OutRelative = tangent };
	}

	// Calculate the tangent for a point on the spline.
	// Tangent is one third of the average of the two vectors from the point to the previous and next points.
	//  --------------p (previous point)
	// /
	// |              o
	// |              |
	// x (point)      |  <- tangent for point t = ((x - p) + (n - x)) * 0.5 * (1/3)
	// |              |
	// |              o
	// \
	//  --------------n (next point)
	private static Vector3 CalculateSmoothTangent( Vector3 previousPoint, Vector3 currentPoint, Vector3 nextPoint )
	{
		var prevToCurrent = currentPoint - previousPoint;
		var currentToNext = nextPoint - currentPoint;
		var tangent = (prevToCurrent + currentToNext) * 0.5f * (1f / 3f);

		return tangent;
	}

	public struct OrientedBoundingBox
	{
		public Transform Transform;
		public Vector3 Extents;
	}

	// Samples spline at regular intervals and calculates the cumulative distance along the spline.
	// Can be used to convert back and forth between distance and spline parameters (tndex, t).
	public class SplineSampler
	{
		private const int SamplesPerSegment = 16;

		private List<float> _cumulativeDistances = new();

		private int _segmentNum;

		private List<BBox> _segmentBounds = new();

		private BBox _bounds;

		public void Sample( ReadOnlyCollection<Spline.Point> spline )
		{
			_segmentNum = SegmentNum( spline );

			float cumulativeLength = 0;
			Vector3 prevPt = SplineUtils.P0( spline, 0 );
			int size = (SamplesPerSegment - 1) * SplineUtils.SegmentNum( spline ) + 2;
			_cumulativeDistances = new List<float>( Enumerable.Repeat( 0.0f, size ) );
			_cumulativeDistances[0] = 0;
			_segmentBounds = new List<BBox>( Enumerable.Repeat( BBox.FromPositionAndSize( prevPt ), _segmentNum ) );
			_bounds = BBox.FromPositionAndSize( prevPt );
			for ( int segmentIndex = 0; segmentIndex < SplineUtils.SegmentNum( spline ); segmentIndex++ )
			{
				var segmentBounds = BBox.FromPositionAndSize( prevPt );
				for ( int sampleIndex = 1; sampleIndex < SamplesPerSegment; sampleIndex++ )
				{
					Vector3 pt = SplineUtils.GetPosition( spline,
						new SegmentParams
						{
							Index = segmentIndex,
							T = sampleIndex / (float)(SamplesPerSegment - 1)
						} );
					cumulativeLength += prevPt.Distance( pt );
					_cumulativeDistances[(segmentIndex * (SamplesPerSegment - 1)) + sampleIndex] = cumulativeLength;

					segmentBounds.Mins = Vector3.Min( _segmentBounds[segmentIndex].Mins, pt );
					segmentBounds.Maxs = Vector3.Max( _segmentBounds[segmentIndex].Maxs, pt );
					prevPt = pt;
				}
				_bounds.Mins = Vector3.Min( _bounds.Mins, _segmentBounds[segmentIndex].Mins );
				_bounds.Maxs = Vector3.Max( _bounds.Maxs, _segmentBounds[segmentIndex].Maxs );
			}
			// duplicate last point this allows (Segment = LastSegment, T = 1) as query in GetDistanceAtSplineParams
			_cumulativeDistances[_cumulativeDistances.Count - 1] = cumulativeLength;
		}

		public SegmentParams CalculateSegmentParamsAtDistance( float distance )
		{
			if ( distance < 0 )
			{
				return new SegmentParams { Index = 0, T = 0 };
			}

			int low = 0;
			int high = _segmentNum - 1;

			while ( low <= high )
			{
				int mid = (low + high) / 2;
				float startDist = GetSegmentStartDistance( mid );
				float endDist = GetSegmentStartDistance( mid + 1 );

				if ( distance < startDist )
				{
					high = mid - 1;
				}
				else if ( distance > endDist )
				{
					low = mid + 1;
				}
				else
				{
					var candidateT = CalculateSegmentTAtDistance( mid, distance );
					if ( candidateT.HasValue )
					{
						return new SegmentParams { Index = mid, T = candidateT.Value };
					}
				}
			}

			return new SegmentParams { Index = _segmentNum - 1, T = 1 };
		}

		public float? CalculateSegmentTAtDistance( int segmentIndex, float distance )
		{
			for ( int sampleIndex = 0; sampleIndex < SamplesPerSegment - 1; sampleIndex++ )
			{
				int distanceIndex = (segmentIndex * (SamplesPerSegment - 1)) + sampleIndex;
				float distPrev = _cumulativeDistances[distanceIndex];
				float distNext = _cumulativeDistances[distanceIndex + 1];
				if ( (distPrev <= distance && distance <= distNext) || distPrev.AlmostEqual( distance, 0.05f ) ||
					 distNext.AlmostEqual( distance, 0.05f ) )
				{
					float tPrev = sampleIndex / (float)(SamplesPerSegment - 1);
					float tNext = (sampleIndex + 1) / (float)(SamplesPerSegment - 1);

					float tMapped = ClampAndRemapValue( distPrev, distNext, tPrev, tNext, distance );
					return tMapped;
				}
			}

			return null;
		}

		public float GetDistanceAtSplineParams( SegmentParams segmentParams )
		{
			//Debug.Assert(segmentParams.T <= 1 && segmentParams.T >= 0);
			//Debug.Assert(segmentParams.Index < _cumulativeDistances.Count - 1);

			int sampleIndex = (int)(segmentParams.T * (SamplesPerSegment - 1));
			int distanceIndex = (segmentParams.Index * (SamplesPerSegment - 1)) + sampleIndex;

			float distPrev = _cumulativeDistances[distanceIndex];
			float distNext = _cumulativeDistances[distanceIndex + 1];

			float tPrev = sampleIndex / (float)(SamplesPerSegment - 1);
			float tNext = (sampleIndex + 1) / (float)(SamplesPerSegment - 1);

			float distanceMapped = ClampAndRemapValue( tPrev, tNext, distPrev, distNext, segmentParams.T );
			return distanceMapped;
		}

		public float GetSegmentStartDistance( int segmentIndex )
		{
			return _cumulativeDistances[segmentIndex * (SamplesPerSegment - 1)];
		}

		public float GetSegmentLength( int segmentIndex )
		{
			return GetSegmentStartDistance( segmentIndex + 1 ) - GetSegmentStartDistance( segmentIndex );
		}

		public BBox GetSegmentBounds( int segmentIndex )
		{
			return _segmentBounds[segmentIndex];
		}

		public BBox GetTotalBounds()
		{
			return _bounds;
		}

		public float TotalLength()
		{
			return _cumulativeDistances.Last();
		}

		// Clamps to the value to [inputMin, inputMax] and afterwards remaps it to the range [outputMin, outputMax].
		private float ClampAndRemapValue( float inputMin, float inputMax, float outputMin, float outputMax,
			float value )
		{
			if ( inputMin.AlmostEqual( inputMax ) )
			{
				return outputMin;
			}

			float clampedValue = Math.Clamp( value, inputMin, inputMax );
			float t = (clampedValue - inputMin) / (inputMax - inputMin);
			return outputMin + t * (outputMax - outputMin);
		}
	}
}

/// <summary>
/// We use a custom converter for <see cref="Spline.Point"/> to allow for more compact serialization.
/// For example we ommit default values for a lot of properties.
/// </summary>
internal class SplinePointConverter : JsonConverter<Spline.Point>
{
	public override Spline.Point Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		if ( reader.TokenType == JsonTokenType.StartObject )
		{
			reader.Read();

			Spline.Point point = new();
			while ( reader.TokenType != JsonTokenType.EndObject )
			{
				if ( reader.TokenType == JsonTokenType.PropertyName )
				{
					var name = reader.GetString();
					reader.Read();

					if ( name == "Pos" )
					{
						point.Position = JsonSerializer.Deserialize<Vector3>( ref reader, options );
					}

					if ( name == "In" )
					{
						point.In = JsonSerializer.Deserialize<Vector3>( ref reader, options );
					}

					if ( name == "Out" )
					{
						point.Out = JsonSerializer.Deserialize<Vector3>( ref reader, options );
					}

					if ( name == "Mode" )
					{
						point.Mode = JsonSerializer.Deserialize<Spline.HandleMode>( ref reader, options );
					}

					if ( name == "Roll" )
					{
						point.Roll = reader.GetSingle();
						reader.Read();
					}

					if ( name == "Scale" )
					{
						point.Scale = JsonSerializer.Deserialize<Vector3>( ref reader, options );
					}

					if ( name == "Up" )
					{
						point.Up = JsonSerializer.Deserialize<Vector3>( ref reader, options );
					}

					continue;

				}

				reader.Read();
			}

			return point;
		}

		return default;
	}

	public override void Write( Utf8JsonWriter writer, Spline.Point val, JsonSerializerOptions options )
	{
		writer.WriteStartObject();

		writer.WritePropertyName( "Pos" );
		JsonSerializer.Serialize( writer, val.Position, options );

		writer.WritePropertyName( "In" );
		JsonSerializer.Serialize( writer, val.In, options );

		writer.WritePropertyName( "Out" );
		JsonSerializer.Serialize( writer, val.Out, options );

		if ( val.Mode != Spline.HandleMode.Auto )
		{
			writer.WritePropertyName( "Mode" );
			JsonSerializer.Serialize( writer, val.Mode, options );
		}

		if ( val.Roll != 0 )
		{
			writer.WritePropertyName( "Roll" );
			writer.WriteNumberValue( val.Roll );
		}

		if ( val.Scale != Vector3.One )
		{
			writer.WritePropertyName( "Scale" );
			JsonSerializer.Serialize( writer, val.Scale, options );
		}

		if ( val.Up != Vector3.Up )
		{
			writer.WritePropertyName( "Up" );
			JsonSerializer.Serialize( writer, val.Up, options );
		}

		writer.WriteEndObject();
	}

}
