using Sandbox.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sandbox;

public sealed class SplineColliderComponent : ModelCollider, Component.ExecuteInEditor
{
	[Property] public SplineComponent Spline { get; set; }

	private static SplineCollisionGeneratorPool collisionGeneratorPool = new( 100 );

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
		}
		base.OnEnabled();
	}

	private void MarkDirty()
	{
		IsDirty = true;
	}

	protected override void OnDisabled()
	{
		Spline.SplineChanged -= MarkDirty;
		meshVertices.Clear();
		meshIndices.Clear();
		base.OnDisabled();
	}

	protected override void OnUpdate()
	{
		if ( !Model.IsValid() || !Spline.IsValid() )
		{
			return;
		}

		if ( !Spline.IsDirty && !IsDirty )
		{
			return;
		}

		// Nothing to deform
		if ( meshVertices.Count == 0 )
		{
			return;
		}

		UpdateCollisions();
	}

	private void UpdateCollisions()
	{
		var sizeInModelDir = Model.Bounds.Size.Dot( Vector3.Forward );

		var minInModelDir = Model.Bounds.Center.Dot( Vector3.Forward ) - sizeInModelDir / 2;

		var meshesRequired = (int)Math.Round( Spline.GetLength() / sizeInModelDir );
		var distancePerCurve = Spline.GetLength() / meshesRequired;

		KeyframeBody.ClearShapes();

		for ( var meshIndex = 0; meshIndex < meshesRequired; meshIndex++ )
		{
			var P0 = Spline.GetPositionAtDistance( meshIndex * distancePerCurve );
			var P1 = P0 + Spline.GetTangetAtDistance( meshIndex * distancePerCurve ) * distancePerCurve / 3;
			var P3 = Spline.GetPositionAtDistance( (meshIndex + 1) * distancePerCurve );
			var P2 = P3 - Spline.GetTangetAtDistance( (meshIndex + 1) * distancePerCurve ) * distancePerCurve / 3;

			var segmentTransform = new Transform( P0, Rotation.LookAt( P3 - P0 ) );
			segmentTransform.Rotation = new Angles( 0, segmentTransform.Rotation.Yaw(), 0 ).ToRotation();

			var rollAtStart = MathX.DegreeToRadian( Spline.GetRollAtDistance( meshIndex * distancePerCurve ) );
			var rollAtEnd = MathX.DegreeToRadian( Spline.GetRollAtDistance( (meshIndex + 1) * distancePerCurve ) );

			var scaleAtStart = Spline.GetScaleAtDistance( meshIndex * distancePerCurve );
			var scaleAtEnd = Spline.GetScaleAtDistance( (meshIndex + 1) * distancePerCurve );

			P1 = new Vector4( segmentTransform.PointToLocal( P1 ) );
			P2 = new Vector4( segmentTransform.PointToLocal( P2 ) );
			P3 = new Vector4( segmentTransform.PointToLocal( P3 ) );

			var RollStartEnd = new Vector2( rollAtStart, rollAtEnd);
			var WidthHeightScaleStartEnd = new Vector4( scaleAtStart.x, scaleAtStart.y, scaleAtEnd.x, scaleAtEnd.y );

			var generator = collisionGeneratorPool.Get().Result;
			generator.SetVertices( meshVertices );
			generator.DeformVertices( minInModelDir, sizeInModelDir, P1, P2, P3, RollStartEnd, WidthHeightScaleStartEnd, segmentTransform );
			collisionGeneratorPool.Return( generator );

			KeyframeBody.AddMeshShape( generator.GetDeformedVertices(), meshIndices );
		}

		IsDirty = false;
	}

	protected override void RebuildImmediately()
	{
		base.RebuildImmediately();

		// TODO there are nicer ways to get the vertices, but that requires internal access and soem engine changes
		// as long as we are in scenestaging hack it
		foreach ( var shape in this.KeyframeBody.Shapes )
		{
			if ( shape.IsMeshShape )
			{
				shape.Triangulate( out var vertices, out var indices );
				meshVertices.AddRange( vertices );
				for ( int i = 0; i < indices.Length; i++ )
				{
					meshIndices.Add( (int)indices[i] );
				}
			}
		}

		MarkDirty();
	}

	private List<Vector3> meshVertices = new();
	private List<int> meshIndices = new();
}

class SplineCollisionGenerator
{
	public void SetVertices( List<Vector3> vertices )
	{
		inVertices.Clear();
		inVertices.AddRange( vertices );
	}

	public List<Vector3> GetDeformedVertices()
	{
		return outVertices;
	}

	public void DeformVertices( float MinInMeshDir, float SizeInMeshDir, Vector3 P1, Vector3 P2, Vector3 P3, Vector2 RollStartEnd, Vector4 WidthHeightScaleStartEnd, Transform SegmentTransform )
	{
		outVertices.Capacity = Math.Max( outVertices.Capacity, inVertices.Count );
		outVertices.Clear();
		for ( int i = 0; i < inVertices.Count; i++ )
		{
			outVertices.Add( SegmentTransform.PointToWorld( SplineModelRendererComponent.DeformVertex( inVertices[i], MinInMeshDir, SizeInMeshDir, P1, P2, P3, RollStartEnd, WidthHeightScaleStartEnd ) ) );
		}
	}

	private List<Vector3> inVertices = new();
	private List<Vector3> outVertices = new();
}


class SplineCollisionGeneratorPool
{

	private readonly ConcurrentBag<SplineCollisionGenerator> generatorInstances;

	private readonly int _maxPoolSize;

	private SemaphoreSlim generatorSemaphore;


	public SplineCollisionGeneratorPool( int maxPoolSize )
	{
		generatorInstances = new ConcurrentBag<SplineCollisionGenerator>();
		generatorSemaphore = new SemaphoreSlim( maxPoolSize );
		_maxPoolSize = maxPoolSize;
		for ( int i = 0; i < _maxPoolSize; i++ )
		{
			generatorInstances.Add( new SplineCollisionGenerator() );
		}
	}

	public async Task<SplineCollisionGenerator> Get()
	{
		ThreadSafe.AssertIsMainThread();
		await generatorSemaphore.WaitAsync();
		var succeeded = generatorInstances.TryTake( out SplineCollisionGenerator generatorInstance );
		Assert.True( succeeded );

		return generatorInstance;
	}

	public void Return( SplineCollisionGenerator generatorInstance )
	{
		ThreadSafe.AssertIsMainThread();
		// not worth to do in parallel it's too fast
		generatorInstances.Add( generatorInstance );
		generatorSemaphore.Release();
	}
}
