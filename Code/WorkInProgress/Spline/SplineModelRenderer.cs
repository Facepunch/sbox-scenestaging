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

		var frames = CalculateTangentFrames( meshesRequired, 16, distancePerMesh );

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
				Deform( vertex.Position, vertex.Normal, vertex.Tangent, frames.GetRange( meshIndex * 16, 16 ), minInModelDir, sizeInModelDir, out deformedVertex.Position, out deformedVertex.Normal, out deformedVertex.Tangent );

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

	private List<Transform> CalculateTangentFrames( int meshesRequired, int frameCount, float distancePerMesh )
	{
		List<Transform> frames = new(meshesRequired * frameCount);

		Vector3 previousTangent = Spline.GetTangetAtDistance( 0f );

		// Initialize up vector
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
		Vector3 up = Vector3.Cross( previousTangent, new Vector3( -previousTangent.y, previousTangent.x, 0f ) ).Normal; float previousRoll = Spline.GetRollAtDistance( 0f );

		// Apply initial roll to up vector
		up = Rotation.FromAxis( previousTangent, previousRoll ) * up;

		for ( var meshIndex = 0; meshIndex < meshesRequired; meshIndex++ )
		{
			float startDistance = meshIndex * distancePerMesh;
			float endDistance = (meshIndex + 1) * distancePerMesh;

			float step = (endDistance - startDistance) / (frameCount - 1);

			for ( int i = 0; i < frameCount; i++ )
			{
				float distance = startDistance + step * i;
				Vector3 position = Spline.GetPositionAtDistance( distance );
				Vector3 tangent = Spline.GetTangetAtDistance( distance );

				// Parallel transport the up vector
				Vector3 transportUp = ParallelTransport( up, previousTangent, tangent );

				// Get roll at the current distance
				float roll = Spline.GetRollAtDistance( distance );

				// Apply roll to the up vector
				float deltaRoll = roll - previousRoll;
				Vector3 finalUp = Rotation.FromAxis( tangent, deltaRoll ) * transportUp;

				// Construct rotation
				Rotation rotation = Rotation.LookAt( tangent, finalUp );

				frames.Add( new Transform( position, rotation ) );

				// Update previous values
				up = finalUp;
				previousTangent = tangent;
				previousRoll = roll;
			}
		}

		return frames;
	}

	private Vector3 ParallelTransport( Vector3 up, Vector3 previousTangent, Vector3 currentTangent )
	{
		Vector3 rotationAxis = Vector3.Cross( previousTangent, currentTangent );
		float dotProduct = Vector3.Dot( previousTangent, currentTangent );
		float angle = MathF.Acos( Math.Clamp( dotProduct, -1f, 1f ) );

		if ( rotationAxis.LengthSquared > 0.0001f && angle > 0.0001f )
		{
			rotationAxis = rotationAxis.Normal;
			up = Rotation.FromAxis( rotationAxis, angle.RadianToDegree() ) * up;
		}

		return up;
	}

	private void Deform( Vector3 localPosition, Vector3 localNormal, Vector4 localTangent, List<Transform> frames, float minInModelDir, float sizeInModelDir, out Vector3 deformedPosition, out Vector3 deformedNormal, out Vector4 deformedTangent )
	{
		// Map localPosition.x to t along the spline segment
		float t = (localPosition.x - minInModelDir) / sizeInModelDir;
		t = Math.Clamp( t, 0f, 1f );

		// Calculate the frame index and interpolation factor
		float frameFloatIndex = t * (frames.Count - 1);
		int frameIndex = (int)Math.Floor( frameFloatIndex );
		float frameT = frameFloatIndex - frameIndex;
		if ( frameIndex >= frames.Count - 1 )
		{
			frameIndex = frames.Count - 2;
			frameT = 1f;
		}

		Transform frame0 = frames[frameIndex];
		Transform frame1 = frames[frameIndex + 1];

		Vector3 position = Vector3.Lerp( frame0.Position, frame1.Position, frameT );
		Rotation rotation = Rotation.Slerp( frame0.Rotation, frame1.Rotation, frameT );


		// Apply model rotation and local offsets
		deformedPosition = position + rotation * (ModelRotation * new Vector3( 0, localPosition.y, localPosition.z ));

		deformedNormal = rotation * (ModelRotation * localNormal);

		deformedTangent = new Vector4( rotation * (ModelRotation * localTangent), localTangent.w);

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
