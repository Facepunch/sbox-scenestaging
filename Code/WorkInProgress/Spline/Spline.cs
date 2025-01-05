using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Sandbox.Spline;

public enum SplinePointTangentMode
{
	/// <summary>
	/// Tangents are calculated automatically
	/// based on the location of adjacent points.
	/// </summary>
	[Icon( "auto_fix_high" )]
	Auto,
	/// <summary>
	/// Tangents are set to zero, leading to a sharp corner.
	/// </summary>
	[Icon( "show_chart" )]
	Linear,
	/// <summary>
	/// The In and Out are user set, but are joined (mirrored)
	/// </summary>
	[Icon( "open_in_full" )]
	Mirrored,
	/// <summary>
	/// The In and Out are user set and operate independently
	/// </summary>
	[Icon( "call_split" )]
	Split,
}

public struct SplinePoint
{
	[JsonInclude]
	public Vector3 Position;

	// The vector between the Position and InPositionRelative forms the tangent at the end of the previous segment.
	[JsonInclude]
	public Vector3 InPositionRelative;

	// The vector between the Position and OutPositionRelative forms the tangent at the start of the next segment.
	[JsonInclude]
	public Vector3 OutPositionRelative;

	[JsonIgnore, Hide]
	public Vector3 OutPosition
	{
		get { return Position + OutPositionRelative; }
		set { OutPositionRelative = value - Position; }
	}

	[JsonIgnore, Hide]
	public Vector3 InPosition
	{
		get { return Position + InPositionRelative; }
		set { InPositionRelative = value - Position; }
	}
}

public struct SplineSegmentParams
{
	public int Index;

	public float T;
}

// Immutable and stateless spline calculations and utilities.
// Will be used by higher level abstractions such as the SplineComponent
// Note: We probably wont want to expose this until the interface is stable
// and we know exactly which of these functions are actually needed/desired
public static class Utils
{
	public static Vector3 GetPosition( ReadOnlyCollection<SplinePoint> spline, SplineSegmentParams segmentParams )
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

	public static Vector3 GetTangent( ReadOnlyCollection<SplinePoint> spline, SplineSegmentParams segmentParams )
	{
		CheckSegmentParams( spline, segmentParams );
		return GetDerivative( spline, segmentParams ).Normal;
	}

	public static Vector3 GetTangent2D( ReadOnlyCollection<SplinePoint> spline, SplineSegmentParams segmentParams )
	{
		CheckSegmentParams( spline, segmentParams );
		return GetDerivative( spline, segmentParams ).WithZ( 0 ).Normal;
	}

	public static Vector3 GetNormal( ReadOnlyCollection<SplinePoint> spline, SplineSegmentParams segmentParams,
		Vector3 up )
	{
		CheckSegmentParams( spline, segmentParams );
		return Vector3.Cross( up, GetTangent( spline, segmentParams ) ).Normal;
	}

	public static Vector3 GetNormal2D( ReadOnlyCollection<SplinePoint> spline, SplineSegmentParams segmentParams )
	{
		CheckSegmentParams( spline, segmentParams );
		Vector3 tangent = GetTangent2D( spline, segmentParams );
		return new Vector3( tangent.y, -tangent.x, 0 );
	}

	public static float GetCurvature( ReadOnlyCollection<SplinePoint> spline, SplineSegmentParams segmentParams )
	{
		CheckSegmentParams( spline, segmentParams );

		var velocity = GetDerivative( spline, segmentParams );
		var velocitySize = velocity.Length;

		var acceleration = GetSecondDerivative( spline, segmentParams );

		var curvatureAxis = Vector3.Cross( velocity, acceleration ) / MathF.Pow( velocitySize, 3 );

		return curvatureAxis.Length;
	}

	public static int SegmentNum( ReadOnlyCollection<SplinePoint> spline )
	{
		return spline.Count - 1;
	}

	private static void DivideSegmentRecursive(
		ReadOnlyCollection<SplinePoint> spline,
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

		var startPosition = GetPosition( spline, new SplineSegmentParams { Index = segmentIndex, T = startT } );
		var middlePosition = GetPosition( spline, new SplineSegmentParams { Index = segmentIndex, T = middleT } );
		var endPosition = GetPosition( spline, new SplineSegmentParams { Index = segmentIndex, T = endT } );

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
		ReadOnlyCollection<SplinePoint> spline,
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
		ReadOnlyCollection<SplinePoint> spline,
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
		outPoints.Capacity = numSegments * averagePointsPerSegmentEstimate;

		var sortedPoints = new SortedDictionary<float, Vector3>();

		for ( int segmentIndex = 0; segmentIndex < numSegments; ++segmentIndex )
		{
			var startDist = sampler.GetSegmentStartDistance( segmentIndex );
			var stopDist = sampler.GetSegmentStartDistance( segmentIndex + 1 );

			sortedPoints.TryAdd( startDist, spline[segmentIndex].Position );
			sortedPoints.TryAdd( stopDist, spline[segmentIndex + 1].Position );

			DivideSegmentRecursive( spline, segmentIndex, sampler, startDist, stopDist, maxSquareError, sortedPoints );
		}

		//a dd start 

		foreach ( var point in sortedPoints )
		{
			outPoints.Add( point.Value );
		}
	}

	private static void CheckSegmentIndex( ReadOnlyCollection<SplinePoint> spline, int index )
	{
		if ( index < 0 || index > SegmentNum( spline ) - 1 )
		{
			throw new ArgumentOutOfRangeException( nameof( index ), "Index is out of range." );
		}
	}

	private static void CheckSegmentParams( ReadOnlyCollection<SplinePoint> spline, SplineSegmentParams segmentParams )
	{
		CheckSegmentIndex( spline, segmentParams.Index );
		if ( segmentParams.T < 0.0f || segmentParams.T > 1.0f )
		{
			throw new ArgumentOutOfRangeException( nameof( segmentParams.T ), "T must be in the range [0.0, 1.0]." );
		}
	}

	private static Vector3 GetDerivative( ReadOnlyCollection<SplinePoint> spline, SplineSegmentParams segmentParams )
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

	private static Vector3 GetSecondDerivative( ReadOnlyCollection<SplinePoint> spline,
		SplineSegmentParams segmentParams )
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

	private static Vector3 GetThirdDerivative( ReadOnlyCollection<SplinePoint> spline, int segmentIndex )
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
	private static List<float> CalculateRootsForAxis( ReadOnlyCollection<SplinePoint> spline, int segmentIndex, int axis )
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
	private static List<float> CalculateLocalExtremaForAxis( ReadOnlyCollection<SplinePoint> spline, int segmentIndex,
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
	public static List<float> CalculateInflections2D( ReadOnlyCollection<SplinePoint> spline, int segmentIndex )
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
	private static void AddExtremaToBox( ReadOnlyCollection<SplinePoint> spline, int segmentIndex, ref BBox inBox )
	{
		for ( int axis = 0; axis < 3; ++axis )
		{
			var extrema = CalculateLocalExtremaForAxis( spline, segmentIndex, axis );
			foreach ( var extremaT in extrema )
			{
				inBox = inBox.AddPoint( GetPosition( spline,
					new SplineSegmentParams { Index = segmentIndex, T = extremaT } ) );
			}
		}
	}

	// The bounding box of a spline segment is defined by the start and end points of the segment and local extrema.
	private static BBox CalculateBoundingBoxForSegment( ReadOnlyCollection<SplinePoint> spline, int segmentIndex )
	{
		BBox result = BBox.FromPositionAndSize( GetPosition( spline, new SplineSegmentParams { Index = segmentIndex, T = 0f } ) );

		result = result.AddPoint( GetPosition( spline, new SplineSegmentParams { Index = segmentIndex, T = 1f } ) );
		AddExtremaToBox( spline, segmentIndex, ref result );

		return result;
	}


	public static bool IsLoop( ReadOnlyCollection<SplinePoint> spline )
	{
		return spline.Count >= 3 && spline[0].Position.AlmostEqual( spline[spline.Count - 1].Position, 0.01f );
	}

	// Calculates the Bounding box of the entire spline, by combining the bounding boxes of each segment.
	public static BBox CalculateBoundingBox( ReadOnlyCollection<SplinePoint> spline )
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

	private static BBox CalculateAlignedBoundingBoxForSegment( ReadOnlyCollection<SplinePoint> spline, int segmentIndex,
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

	public static OrientedBoundingBox CalculateMinOrientedBoundingBox( ReadOnlyCollection<SplinePoint> spline )
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

	private static double CalculateArea2DForSegment( ReadOnlyCollection<SplinePoint> spline, int segmentIndex )
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

	public static double CalculateArea2D( ReadOnlyCollection<SplinePoint> spline )
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

	public static Vector3 P0( ReadOnlyCollection<SplinePoint> spline, int segmentIndex )
	{
		CheckSegmentIndex( spline, segmentIndex );
		return spline[segmentIndex].Position;
	}

	public static Vector3 P1( ReadOnlyCollection<SplinePoint> spline, int segmentIndex )
	{
		CheckSegmentIndex( spline, segmentIndex );
		return spline[segmentIndex].OutPosition;
	}

	public static Vector3 P2( ReadOnlyCollection<SplinePoint> spline, int segmentIndex )
	{
		CheckSegmentIndex( spline, segmentIndex );
		return spline[segmentIndex + 1].InPosition;
	}

	public static Vector3 P3( ReadOnlyCollection<SplinePoint> spline, int segmentIndex )
	{
		CheckSegmentIndex( spline, segmentIndex );
		return spline[segmentIndex + 1].Position;
	}

	public struct SplitSegmentResult
	{
		public SplinePoint Left;
		public SplinePoint Mid;
		public SplinePoint Right;
	}

	// https://www.youtube.com/watch?v=lPJo1jayLdc
	public static SplitSegmentResult SplitSegment( ReadOnlyCollection<SplinePoint> spline, SplineSegmentParams segmentParams )
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
			Left = new SplinePoint
			{
				Position = point0,
				InPositionRelative = spline[segmentParams.Index].InPositionRelative,
				OutPositionRelative = leftOut - point0,
			},
			Mid = new SplinePoint
			{
				Position = midPoint,
				InPositionRelative = midIn - midPoint,
				OutPositionRelative = midOut - midPoint,
			},
			Right = new SplinePoint
			{
				Position = point3,
				InPositionRelative = rightIn - point3,
				OutPositionRelative = spline[segmentParams.Index + 1].OutPositionRelative,
			}
		};

		return result;
	}

	// https://github.com/erich666/GraphicsGems/blob/master/gems/FitCurves.c
	public static SplineSegmentParams FindSegmentAndTClosestToPosition(
		ReadOnlyCollection<SplinePoint> spline,
		Vector3 queryPosition )
	{
		const int pointsChecked = 3;
		const int iterationNum = 3;

		SplineSegmentParams result = new SplineSegmentParams();
		float closestDistanceSq = float.MaxValue;

		// TODO convert to stack alloc
		float[] initialTs = new float[pointsChecked] { 0.0f, 0.5f, 1.0f };

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
		ReadOnlyCollection<SplinePoint> spline,
		int segmentIndex,
		Vector3 queryPosition,
		float currentCandidate,
		int iterationNum )
	{
		Vector3 delta = Vector3.Zero;
		for ( int iteration = 0; iteration < iterationNum; ++iteration )
		{
			Vector3 position = GetPosition( spline, new SplineSegmentParams { Index = segmentIndex, T = currentCandidate } );
			Vector3 firstDerivative = GetDerivative( spline, new SplineSegmentParams { Index = segmentIndex, T = currentCandidate } );
			Vector3 secondDerivative = GetSecondDerivative( spline, new SplineSegmentParams { Index = segmentIndex, T = currentCandidate } );
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
	private static SplinePoint[] AlignSegment( ReadOnlyCollection<SplinePoint> spline, int segmentIndex,
		SegmentAlignmentInfo alignmentInfo )
	{
		var alignedSegment = new SplinePoint[2];
		alignedSegment[0].Position = alignmentInfo.Rotation * (spline[segmentIndex].Position + alignmentInfo.Translation);
		alignedSegment[0].InPositionRelative = Vector3.Zero;
		alignedSegment[0].OutPositionRelative = alignmentInfo.Rotation * spline[segmentIndex].OutPositionRelative;

		alignedSegment[1].Position =
			alignmentInfo.Rotation * (spline[segmentIndex + 1].Position + alignmentInfo.Translation);
		alignedSegment[1].InPositionRelative = alignmentInfo.Rotation * spline[segmentIndex + 1].InPositionRelative;
		alignedSegment[1].OutPositionRelative = Vector3.Zero;

		return alignedSegment;
	}

	// Calculates a linear tangent for a point on the spline and returns a new spline point with the tangent.
	public static SplinePoint CalculateLinearTangentForPoint( ReadOnlyCollection<SplinePoint> spline, int pointIndex )
	{
		return spline[pointIndex] with { InPositionRelative = Vector3.Zero, OutPositionRelative = Vector3.Zero };
	}

	// Calculate a smooth tangent for a point on the spline and return new spline point with the tangent.
	public static SplinePoint CalculateSmoothTangentForPoint( ReadOnlyCollection<SplinePoint> spline, int pointIndex )
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

		return spline[pointIndex] with { InPositionRelative = -tangent, OutPositionRelative = tangent };
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

		private float[] _cumulativeDistances;

		private int _segmentNum;

		private BBox[] _segmentBounds;

		private BBox _bounds;

		public void Sample( ReadOnlyCollection<SplinePoint> spline )
		{
			_segmentNum = SegmentNum( spline );

			float cumulativeLength = 0;
			Vector3 prevPt = Utils.P0( spline, 0 );
			int size = (SamplesPerSegment - 1) * Utils.SegmentNum( spline ) + 2;
			_cumulativeDistances = new float[size];
			_cumulativeDistances[0] = 0;
			_segmentBounds = new BBox[_segmentNum];
			_bounds = BBox.FromPositionAndSize( prevPt );
			for ( int segmentIndex = 0; segmentIndex < Utils.SegmentNum( spline ); segmentIndex++ )
			{
				_segmentBounds[segmentIndex] = BBox.FromPositionAndSize( prevPt );
				for ( int sampleIndex = 1; sampleIndex < SamplesPerSegment; sampleIndex++ )
				{
					Vector3 pt = Utils.GetPosition( spline,
						new SplineSegmentParams
						{
							Index = segmentIndex,
							T = sampleIndex / (float)(SamplesPerSegment - 1)
						} );
					cumulativeLength += prevPt.Distance( pt );
					_cumulativeDistances[(segmentIndex * (SamplesPerSegment - 1)) + sampleIndex] = cumulativeLength;
					_segmentBounds[segmentIndex].Mins = Vector3.Min( _segmentBounds[segmentIndex].Mins, pt );
					_segmentBounds[segmentIndex].Maxs = Vector3.Max( _segmentBounds[segmentIndex].Maxs, pt );
					prevPt = pt;
				}
				_bounds.Mins = Vector3.Min( _bounds.Mins, _segmentBounds[segmentIndex].Mins );
				_bounds.Maxs = Vector3.Max( _bounds.Maxs, _segmentBounds[segmentIndex].Maxs );
			}
			// duplicate last point this allows (Segment = LastSegment, T = 1) as query in GetDistanceAtSplineParams
			_cumulativeDistances[_cumulativeDistances.Length - 1] = cumulativeLength;
		}

		public SplineSegmentParams CalculateSegmentParamsAtDistance( float distance )
		{
			if ( distance < 0 )
			{
				return new SplineSegmentParams { Index = 0, T = 0 };
			}

			for ( int segmentIndex = 0; segmentIndex < _segmentNum; segmentIndex++ )
			{
				var candidateT = CalculateSegmentTAtDistance( segmentIndex, distance );
				if ( candidateT.HasValue )
				{
					return new SplineSegmentParams { Index = segmentIndex, T = candidateT.Value };
				}
			}

			return new SplineSegmentParams { Index = _segmentNum - 1, T = 1 };
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

		public float GetDistanceAtSplineParams( SplineSegmentParams segmentParams )
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
