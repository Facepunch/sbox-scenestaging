using Sandbox.Diagnostics;
using Sandbox.Rendering;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sandbox;

public sealed class SplineModelRendererComponent : Component, Component.ExecuteInEditor
{
	[Property] public SplineComponent Spline { get; set; }

	[Property] public Model Model { get; set; }

	[Property] public Rotation ModelRotation { get; set; } = Rotation.Identity;

	private SceneObject sceneObject;
	private Mesh customMesh = new();
	private Model customModel = Model.Error;

	List<Vertex> deformedVertices = new();
	private List<int> deformedIndices = new();

	private bool IsDirty
	{
		get => _isDirty; set => _isDirty = value;
	}

	private bool _isDirty = true;

	protected override void OnEnabled()
	{
		if ( Model.IsValid() && Spline.IsValid() )
		{
			IsDirty = true;
			Spline.SplineChanged += MarkDirty;
			Transform.OnTransformChanged += OnTransformChanged;

			// Create a new SceneObject and Mesh
			customMesh = new();
			customMesh.Material = Model.Materials.FirstOrDefault();
			sceneObject = new SceneObject( Scene.SceneWorld, Model );
			sceneObject.Transform = WorldTransform;
			sceneObject.SetComponentSource( this );
			sceneObject.Tags.SetFrom( GameObject.Tags );
		}
	}

	private void MarkDirty()
	{
		IsDirty = true;
	}

	private void OnTransformChanged()
	{
		if ( sceneObject != null )
		{
			sceneObject.Transform = WorldTransform;
		}
	}

	protected override void OnDisabled()
	{
		if ( sceneObject != null )
		{
			sceneObject.Delete();
			sceneObject = null;
			customMesh = null;
		}

		Spline.SplineChanged -= MarkDirty;
		Transform.OnTransformChanged -= OnTransformChanged;
	}

	protected override void OnPreRender()
	{
		if ( !Model.IsValid() || !Spline.IsValid() )
		{
			return;
		}

		if ( !Spline.IsDirty && !IsDirty )
		{
			return;
		}

		UpdateRenderState();
	}

	protected override void DrawGizmos()
	{
		var rotatedModelBounds = Model.Bounds.Rotate( ModelRotation );
		var sizeInModelDir = rotatedModelBounds.Size.Dot( Vector3.Forward );

		var minInModelDir = rotatedModelBounds.Center.Dot( Vector3.Forward ) - sizeInModelDir / 2;

		var splineLength = Spline.GetLength();
		var meshesRequired = (int)Math.Ceiling( splineLength / sizeInModelDir );
		var distancePerMesh = splineLength / meshesRequired;

		var frames = CalculateTangentFramesUsingUpDir( meshesRequired, 16, distancePerMesh );

		float arrowLength = distancePerMesh / 4;

		foreach ( var frame in frames )
		{
			var position = frame.Position;
			var tangent = frame.Forward;
			var finalUp = frame.Up;
			var right = frame.Right;

			// Draw tangent vector (forward)
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.Arrow( position, position + tangent * arrowLength, arrowLength / 10f, arrowLength / 15f );

			// Draw up vector (normal)
			Gizmo.Draw.Color = Color.Green;
			Gizmo.Draw.Arrow( position, position + finalUp * arrowLength, arrowLength / 10f, arrowLength / 15f );

			// Draw right vector (binormal)
			Gizmo.Draw.Color = Color.Blue;
			Gizmo.Draw.Arrow( position, position + right * arrowLength, arrowLength / 10f, arrowLength / 15f );
		}
	}

	private void UpdateRenderState()
	{
		var rotatedModelBounds = Model.Bounds.Rotate( ModelRotation );
		var sizeInModelDir = rotatedModelBounds.Size.Dot( Vector3.Forward );

		var minInModelDir = rotatedModelBounds.Center.Dot( Vector3.Forward ) - sizeInModelDir / 2;

		var splineLength = Spline.GetLength();
		var meshesRequired = (int)Math.Ceiling( splineLength / sizeInModelDir );
		var distancePerMesh = splineLength / meshesRequired;

		if ( meshesRequired == 0 )
		{
			return;
		}

		// Get the mesh from the model
		var modelVertices = Model.GetVertices();
		var modelIndices = Model.GetIndices();

		deformedVertices.Capacity = Math.Max(modelVertices.Count() * meshesRequired, deformedVertices.Capacity);
		deformedVertices.Clear();

		deformedIndices.Capacity = Math.Max( modelIndices.Count() * meshesRequired, deformedIndices.Capacity);
		deformedIndices.Clear();

		var frames = CalculateTangentFramesUsingUpDir( meshesRequired, 16, distancePerMesh );

		int vertexOffset = 0;

		for ( var meshIndex = 0; meshIndex < meshesRequired; meshIndex++ )
		{
			float startDistance = meshIndex * distancePerMesh;
			float endDistance = (meshIndex + 1) * distancePerMesh;

			float segmentLength = endDistance - startDistance;

			// Deform vertices for this segment
			for ( int i = 0; i < modelVertices.Length; i++ )
			{
				var vertex = modelVertices[i];

				var deformedVertex = vertex;

				// Deform the vertex using tangent frames
				Deform( vertex.Position, vertex.Normal, vertex.Tangent, frames.GetRange( Math.Max( meshIndex * 16 - 1, 0), meshIndex == 0 ? 16 : 17 ), minInModelDir, sizeInModelDir, out deformedVertex.Position, out deformedVertex.Normal, out deformedVertex.Tangent );

				deformedVertices.Add( deformedVertex );
			}

			// Copy indices for this segment
			for ( int i = 0; i < modelIndices.Length; i++ )
			{
				deformedIndices.Add((int)(modelIndices[i] + vertexOffset));
			}

			vertexOffset += modelVertices.Length;
		}

		if ( customMesh.HasVertexBuffer )
		{
			customMesh.SetIndexBufferSize( deformedIndices.Count );
			customMesh.SetVertexBufferSize( deformedVertices.Count );
			customMesh.SetIndexBufferData( deformedIndices );
			customMesh.SetVertexBufferData( deformedVertices );
		}
		else
		{
			customMesh.CreateVertexBuffer( deformedVertices.Count, Vertex.Layout, deformedVertices );
			customMesh.CreateIndexBuffer( deformedIndices.Count, deformedIndices );
		}

		customModel = Model.Builder.AddMesh( customMesh ).Create();
		sceneObject.Model = customModel;

		IsDirty = false;
	}

	private List<Transform> CalculateTangentFramesUsingUpDir( int meshesRequired, int frameCount, float distancePerMesh  )
	{
		int totalFrames = meshesRequired * frameCount;
		List<Transform> frames = new( totalFrames );

		float totalSplineLength = Spline.GetLength();

		Vector3 initialTangent = Spline.GetTangetAtDistance( 0f );
		Vector3 up = Spline.GetUpVectorAtDistance( 0f );

		// Choose an initial up vector if tangent is parallel to Up
		if ( MathF.Abs( Vector3.Dot( initialTangent, up ) ) > 0.999f )
		{
			up = Vector3.Right;
		}

		for ( int i = 0; i < totalFrames; i++ )
		{
			float t = (float)i / (totalFrames - 1);
			float distance = t * totalSplineLength;

			Vector3 position = Spline.GetPositionAtDistance( distance );
			Vector3 tangent = Spline.GetTangetAtDistance( distance );
			up = Spline.GetUpVectorAtDistance( distance );

			// Apply roll
			float roll = Spline.GetRollAtDistance( distance );
			var newUp = Rotation.FromAxis( tangent, roll ) * up;

			Rotation rotation = Rotation.LookAt( tangent, newUp );

			// Get scale for y and z directions from the spline
			Vector2 scale2D = Spline.GetScaleAtDistance( distance );

			// Create scale vector with 1 for x (since we're scaling along y and z)
			Vector3 scale = new Vector3( 1f, scale2D.x, scale2D.y );

			frames.Add( new Transform( position, rotation, scale ) );
		}

		return frames;
	}

	private List<Transform> CalculateRotationMinimizingTangentFrames( int meshesRequired, int frameCount, float distancePerMesh )
	{
		int totalFrames = meshesRequired * frameCount;
		List<Transform> frames = new( totalFrames );

		float totalSplineLength = Spline.GetLength();

		// Initialize the up vector
		Vector3 previousTangent = Spline.GetTangetAtDistance( 0f );
		Vector3 up = Vector3.Up;

		// Choose an initial up vector if tangent is parallel to Up
		if ( MathF.Abs( Vector3.Dot( previousTangent, up ) ) > 0.999f )
		{
			up = Vector3.Right;
		}

		float previousRoll = Spline.GetRollAtDistance( 0f );
		up = Rotation.FromAxis( previousTangent, previousRoll ) * up;

		Vector3 previousPosition = Spline.GetPositionAtDistance( 0f );
		frames.Add( new Transform( previousPosition, Rotation.LookAt( previousTangent, up ) ) );

		for ( int i = 1; i < totalFrames; i++ )
		{
			float t = (float)i / (totalFrames - 1);
			float distance = t * totalSplineLength;

			Vector3 position = Spline.GetPositionAtDistance( distance );
			Vector3 tangent = Spline.GetTangetAtDistance( distance );

			// Parallel transport the up vector
			up = GetRotationMinimizingNormal( previousPosition, previousTangent, up, position, tangent );

			// Apply roll
			float roll = Spline.GetRollAtDistance( distance );
			float deltaRoll = roll - previousRoll;
			up = Rotation.FromAxis( tangent, deltaRoll ) * up;

			Rotation rotation = Rotation.LookAt( tangent, up );

			// Get scale for y and z directions from the spline
			Vector2 scale2D = Spline.GetScaleAtDistance( distance );

			// Create scale vector with 1 for x (since we're scaling along y and z)
			Vector3 scale = new Vector3( 1f, scale2D.x, scale2D.y );

			frames.Add( new Transform( position, rotation, scale ) );

			previousTangent = tangent;
			previousPosition = position;
			previousRoll = roll;
		}

		// Correct up vectors for looped splines
		if (Spline.IsLoop && frames.Count > 1)
		{
			Vector3 startUp = frames[0].Rotation.Up;
			Vector3 endUp = frames[^1].Rotation.Up;

			float theta = MathF.Acos(Vector3.Dot(startUp, endUp)) / (frames.Count - 1);
			if (Vector3.Dot(frames[0].Rotation.Forward, Vector3.Cross(startUp, endUp)) > 0)
			{
				theta = -theta;
			}

			for (int i = 0; i < frames.Count; i++)
			{
				Rotation R = Rotation.FromAxis(frames[i].Rotation.Forward, (theta * i).RadianToDegree());
				Vector3 correctedUp = R * frames[i].Rotation.Up;
				frames[i] = new Transform(frames[i].Position, Rotation.LookAt(frames[i].Rotation.Forward, correctedUp), frames[i].Scale );
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

	private void Deform( Vector3 localPosition, Vector3 localNormal, Vector4 localTangent, List<Transform> frames, float minInModelDir, float sizeInModelDir, out Vector3 deformedPosition, out Vector3 deformedNormal, out Vector4 deformedTangent )
	{
		// Map localPosition.x to t along the spline segment
		float t = (localPosition.x - minInModelDir) / sizeInModelDir;
		t = Math.Clamp( t, 0f, 1f );

		// Calculate the frame index and interpolation factor
		float frameFloatIndex = t * (frames.Count - 1);
		int frameIndex = Math.Clamp( (int)Math.Floor( frameFloatIndex ), 0, frames.Count - 2 );
		float frameT = Math.Clamp( frameFloatIndex - frameIndex, 0f, 1f );

		Transform frame0 = frames[frameIndex];
		Transform frame1 = frames[frameIndex + 1];

		Vector3 position = Vector3.Lerp( frame0.Position, frame1.Position, frameT );
		Rotation rotation = Rotation.Slerp( frame0.Rotation, frame1.Rotation, frameT );

		// Interpolate scale from frames
		Vector3 scale0 = frame0.Scale;
		Vector3 scale1 = frame1.Scale;
		Vector3 scale = Vector3.Lerp( scale0, scale1, frameT );

		// Scale localPosition along y and z axes
		Vector3 scaledLocalPosition = new Vector3( 0, localPosition.y * scale.y, localPosition.z * scale.z );

		// Apply model rotation and local offsets
		deformedPosition = position + rotation * (ModelRotation * scaledLocalPosition);

		deformedNormal = rotation * (ModelRotation * localNormal);
		deformedTangent = new Vector4( rotation * (ModelRotation * localTangent), localTangent.w );
	}

	/// LEGACY STUFF
	public static Vector3 DeformVertex( Vector3 localPosition, float MinInMeshDir, float SizeInMeshDir, Vector3 P1, Vector3 P2, Vector3 P3, Vector2 RollStartEnd, Vector4 WidthHeightScaleStartEnd )
	{
		float t = (localPosition.x - MinInMeshDir) / SizeInMeshDir;



		Vector3 p0 = Vector3.Zero;
		Vector3 p1 = P1;
		Vector3 p2 = P2;
		Vector3 p3 = P3;

		float rollStart = RollStartEnd.x;
		float rollEnd = RollStartEnd.y;

		float roll = MathX.Lerp( rollStart, rollEnd, t );





		Vector2 startScaleWidthHeight = new Vector2( WidthHeightScaleStartEnd.x, WidthHeightScaleStartEnd.y );
		Vector2 endScaleWidthHeight = new Vector2( WidthHeightScaleStartEnd.z, WidthHeightScaleStartEnd.w );

		Vector2 scale = Vector2.Lerp( startScaleWidthHeight, endScaleWidthHeight, t );


		Vector3 up = Vector3.Up;
		Vector3 forward = CalculateBezierTangent( t, p0, p1, p2, p3 );
		Vector3 right = Vector3.Cross( up, forward ).Normal;
		up = Vector3.Cross( forward, right ).Normal;

		float sine = MathF.Sin( roll );
		float cosine = MathF.Cos( roll );
		Vector3 rightRotated = cosine * right - sine * up;
		Vector3 upRotated = sine * right + cosine * up;

		Vector3 curvePosition = CalculateBezierPosition( t, p0, p1, p2, p3 );
		Vector3 deformedPosition = ScaleAndRotateVector( localPosition, scale, rightRotated, upRotated );

		return curvePosition + deformedPosition;
	}




	private static Vector3 CalculateBezierPosition( float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3 )
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




	private static Vector3 CalculateBezierTangent( float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3 )
	{
		float t2 = t * t;

		float w0 = -3 * t2 + 6 * t - 3;
		float w1 = 9 * t2 - 12 * t + 3;
		float w2 = -9 * t2 + 6 * t;
		float w3 = 3 * t2;


		return w0 * p0 + w1 * p1 + w2 * p2 + w3 * p3;
	}



	private static Vector3 ScaleAndRotateVector( Vector3 localPosition, Vector2 scale, Vector3 right, Vector3 up )
	{
		Vector3 scaledRight = right * scale.x;
		Vector3 scaledUp = up * scale.y;

		return localPosition.y * scaledRight + localPosition.z * scaledUp;
	}
}
