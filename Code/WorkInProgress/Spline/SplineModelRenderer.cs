using Sandbox.Rendering;
using System.Runtime.InteropServices;

namespace Sandbox;

public sealed class SplineModelRendererComponent : Component, Component.ExecuteInEditor
{
	[Property] public SplineComponent Spline { get; set; }

	[Property] public Model Model { get; set; }

	[StructLayout( LayoutKind.Sequential, Pack = 0 )]
	private struct GpuSplineSegment
	{
		// public Vector4 P0; // P0 is ommited because it is always 0,0,0
		public Vector4 P1;
		public Vector4 P2;
		public Vector4 P3;
		public Vector4 RollStartEnd;
		public Vector4 WidthHeightScaleStartEnd;
	}

	private List<SceneObject> sceneObjects;

	private bool IsDirty
	{
		get => _isDirty; set => _isDirty = value;
	}

	private bool _isDirty = true;

	protected override void OnEnabled()
	{
		if ( Model.IsValid() && Spline.IsValid() )
		{
			sceneObjects = new();
			IsDirty = true;
			Spline.SplineChanged += MarkDirty;
		}
	}

	private void MarkDirty()
	{
		IsDirty = true;
	}

	protected override void OnDisabled()
	{
		foreach ( var sceneObject in sceneObjects )
		{
			sceneObject.Delete();
		}
		sceneObjects.Clear();
		Spline.SplineChanged -= MarkDirty;
	}

	protected override void OnPreRender()
	{
		for ( var meshIndex = 0; meshIndex < sceneObjects.Count; meshIndex++ )
		{
			DebugOverlay.Box( sceneObjects[meshIndex].Bounds );
		}

		if ( !Model.IsValid() || !Spline.IsValid() || !Spline.IsDirty || !IsDirty )
		{
			return;
		}

		UpdateRenderState();
	}

	private void UpdateRenderState()
	{
		Log.Info( "UpdateRenderState" );

		var sizeInModelDir = Model.Bounds.Size.Dot( Vector3.Forward );

		var minInModelDir = Model.Bounds.Center.Dot( Vector3.Forward ) - sizeInModelDir / 2;

		var meshesRequired = (int)Math.Round( Spline.GetLength() / sizeInModelDir );
		var distancePerCurve = Spline.GetLength() / meshesRequired;

		// TODO handle somehow
		if ( meshesRequired == 0 )
		{
			return;
		}

		// create enough sceneobjects
		for ( int i = sceneObjects.Count; i < meshesRequired; i++ )
		{
			var sceneObject = new SceneObject( Scene.SceneWorld, Model );
			sceneObject.Transform = WorldTransform;
			sceneObject.SetComponentSource( this );
			sceneObject.Tags.SetFrom( GameObject.Tags );
			sceneObjects.Add( sceneObject );
		}

		// delete if there are too many
		for ( int i = sceneObjects.Count - 1; i >= meshesRequired; i-- )
		{
			sceneObjects[i].Delete();
			sceneObjects.RemoveAt( i );
		}

		for ( var meshIndex = 0; meshIndex < meshesRequired; meshIndex++ )
		{
			var P0 = Spline.GetPositionAtDistance( meshIndex * distancePerCurve );
			var P1 = P0 + Spline.GetTangetAtDistance( meshIndex * distancePerCurve ) * distancePerCurve / 3;
			var P3 = Spline.GetPositionAtDistance( (meshIndex + 1) * distancePerCurve );
			var P2 = P3 - Spline.GetTangetAtDistance( (meshIndex + 1) * distancePerCurve ) * distancePerCurve / 3;

			// convert to worldspace
			var P0World = Spline.WorldTransform.PointToWorld( P0 );
			var P1World = Spline.WorldTransform.PointToWorld( P1 );
			var P2World = Spline.WorldTransform.PointToWorld( P2 );
			var P3World = Spline.WorldTransform.PointToWorld( P3 );

			var segmentTransform = new Transform( P0World, Rotation.LookAt( P3World - P0World ) );
			segmentTransform.Rotation = new Angles( 0, segmentTransform.Rotation.Yaw(), 0 ).ToRotation();

			var rollAtStart = MathX.DegreeToRadian( Spline.GetRollAtDistance( meshIndex * distancePerCurve ) );
			var rollAtEnd = MathX.DegreeToRadian( Spline.GetRollAtDistance( (meshIndex + 1) * distancePerCurve ) );

			var scaleAtStart = Spline.GetScaleAtDistance( meshIndex * distancePerCurve );
			var scaleAtEnd = Spline.GetScaleAtDistance( (meshIndex + 1) * distancePerCurve );

			var segment = new GpuSplineSegment
			{
				P1 = new Vector4( segmentTransform.PointToLocal( P1World ) ),
				P2 = new Vector4( segmentTransform.PointToLocal( P2World ) ),
				P3 = new Vector4( segmentTransform.PointToLocal( P3World ) ),
				RollStartEnd = new Vector4( rollAtStart, rollAtEnd, 0, 0 ),
				WidthHeightScaleStartEnd = new Vector4( scaleAtStart.x, scaleAtStart.y, scaleAtEnd.x, scaleAtEnd.y )
			};

			// TODO pack this more efficiently
			sceneObjects[meshIndex].Attributes.Set( "P1", segment.P1 );
			sceneObjects[meshIndex].Attributes.Set( "P2", segment.P2 );
			sceneObjects[meshIndex].Attributes.Set( "P3", segment.P3 );
			sceneObjects[meshIndex].Attributes.Set( "RollStartEnd", segment.RollStartEnd );
			sceneObjects[meshIndex].Attributes.Set( "WidthHeightScaleStartEnd", segment.WidthHeightScaleStartEnd );
			sceneObjects[meshIndex].Attributes.Set( "MinInModelDir", minInModelDir );
			sceneObjects[meshIndex].Attributes.Set( "SizeInModelDir", sizeInModelDir );

			// :(
			sceneObjects[meshIndex].Batchable = false;

			sceneObjects[meshIndex].Transform = segmentTransform;

			var newBounds = new BBox( Vector3.Zero, Vector3.Zero );

			foreach ( var corner in Model.Bounds.Corners )
			{
				var deformed = DeformVertex( corner, minInModelDir, sizeInModelDir, segment.P1, segment.P2, segment.P3, new Vector2( rollAtStart, rollAtEnd ), segment.WidthHeightScaleStartEnd );
				newBounds = newBounds.AddPoint( deformed );
			}

			newBounds = newBounds.Rotate( segmentTransform.Rotation );

			sceneObjects[meshIndex].LocalBounds = newBounds;
		}

		IsDirty = false;
	}

    public static Vector3 DeformVertex(Vector3 localPosition, float MinInMeshDir, float SizeInMeshDir, Vector3 P1, Vector3 P2, Vector3 P3, Vector2 RollStartEnd, Vector4 WidthHeightScaleStartEnd)
    {
        float t = (localPosition.x - MinInMeshDir) / SizeInMeshDir;

        Vector3 p0 = Vector3.Zero;
        Vector3 p1 = P1;
        Vector3 p2 = P2;
        Vector3 p3 = P3;

        float rollStart = RollStartEnd.x;
        float rollEnd = RollStartEnd.y;

        float roll = MathX.Lerp(rollStart, rollEnd, t);

        Vector2 startScaleWidthHeight = new Vector2(WidthHeightScaleStartEnd.x, WidthHeightScaleStartEnd.y);
        Vector2 endScaleWidthHeight = new Vector2(WidthHeightScaleStartEnd.z, WidthHeightScaleStartEnd.w);

        Vector2 scale = Vector2.Lerp(startScaleWidthHeight, endScaleWidthHeight, t);

        Vector3 up = Vector3.Up;
        Vector3 forward = CalculateBezierTangent(t, p0, p1, p2, p3);
        Vector3 right = Vector3.Cross(up, forward).Normal;
        up = Vector3.Cross(forward, right).Normal;

        float sine = MathF.Sin(roll);
        float cosine = MathF.Cos(roll);
        Vector3 rightRotated = cosine * right - sine * up;
        Vector3 upRotated = sine * right + cosine * up;

        Vector3 curvePosition = CalculateBezierPosition(t, p0, p1, p2, p3);
        Vector3 deformedPosition = ScaleAndRotateVector(localPosition, scale, rightRotated, upRotated);

        return curvePosition + deformedPosition;
    }

    private static Vector3 CalculateBezierPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float tSquare = t * t;
        float tCubic = tSquare * t;
        float oneMinusT = 1 - t;
        float oneMinusTSquare = oneMinusT * oneMinusT;
        float oneMinusTCubic = oneMinusTSquare * oneMinusT;

        float w0 = oneMinusTCubic;
        float w1 = 3 * oneMinusTSquare * t;
        float w2 = 3 * oneMinusT * tSquare;
        float w3 = tCubic;

        return w0 * p0 + w1 * p1 + w2 * p2 + w3 * p3;
    }

    private static Vector3 CalculateBezierTangent(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float t2 = t * t;

        float w0 = -3 * t2 + 6 * t - 3;
        float w1 = 9 * t2 - 12 * t + 3;
        float w2 = -9 * t2 + 6 * t;
        float w3 = 3 * t2;

        return w0 * p0 + w1 * p1 + w2 * p2 + w3 * p3;
    }

    private static Vector3 ScaleAndRotateVector(Vector3 localPosition, Vector2 scale, Vector3 right, Vector3 up)
    {
        Vector3 scaledRight = right * scale.x;
        Vector3 scaledUp = up * scale.y;

        return localPosition.y * scaledRight + localPosition.z * scaledUp;
    }
}
