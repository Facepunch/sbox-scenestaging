using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

public sealed class Glass : Component, Component.ExecuteInEditor, Component.IDamageable
{
	private sealed class Shard : IValid
	{
		public SceneObject SceneObject { get; private set; }
		public PhysicsBody PhysicsBody { get; private set; }
		public PhysicsShape PhysicsShape { get; private set; }
		public Vector2[] Points { get; private set; }
		public float Area { get; private set; }
		public bool IsLoose { get; set; }
		public TimeSince TimeCreated { get; private set; }

		public bool IsValid => SceneObject.IsValid() && PhysicsBody.IsValid();

		public Shard( SceneObject sceneObject, PhysicsShape shape, Vector2[] points )
		{
			SceneObject = sceneObject;
			PhysicsBody = shape.Body;
			PhysicsShape = shape;
			Points = points;
			TimeCreated = 0;
			Area = CalculateArea();
		}

		public void Destroy()
		{
			SceneObject?.Delete();
			PhysicsBody?.Remove();
			PhysicsBody = null;
		}

		public bool IsPointInside( Vector2 point )
		{
			if ( Points == null || Points.Length < 3 )
				return false;

			int positive = 0;
			int negative = 0;

			for ( int i = 0; i < Points.Length; i++ )
			{
				var v1 = Points[i];
				var v2 = Points[i < Points.Length - 1 ? i + 1 : 0];

				float cross = (point.x - v1.x) * (v2.y - v1.y) - (point.y - v1.y) * (v2.x - v1.x);

				if ( cross > 0 )
				{
					positive++;
				}
				else if ( cross < 0 )
				{
					negative++;
				}

				if ( positive > 0 && negative > 0 )
				{
					return false;
				}
			}

			return true;
		}

		private float CalculateArea()
		{
			var area = 0.0f;

			if ( Points is not null && Points.Length >= 3 )
			{
				var v1 = Points[0];

				for ( var i = 1; i < Points.Length - 1; i++ )
				{
					var v2 = Points[i];
					var v3 = Points[i + 1];
					var x1 = v2.x - v1.x;
					var y1 = v2.y - v1.y;
					var x2 = v3.x - v1.x;
					var y2 = v3.y - v1.y;

					area += MathF.Abs( x1 * y2 - x2 * y1 );
				}

				area = MathF.Abs( area * 0.5f );
			}

			return area;
		}
	}

	private readonly Dictionary<PhysicsShape, Shard> Shards = new();
	private readonly List<PhysicsShape> ShardsToRemove = new();

	[Property, MakeDirty] public Material Material { get; set; }
	[Property, MakeDirty] public Surface Surface { get; set; }
	[Property, MakeDirty] public float Thickness { get; set; } = 1;
	[Property, MakeDirty] public Vector3 TextureAxisU { get; set; } = Vector3.Forward;
	[Property, MakeDirty] public Vector3 TextureAxisV { get; set; } = Vector3.Right;
	[Property, MakeDirty] public Vector2 TextureScale { get; set; } = 1;
	[Property, MakeDirty] public Vector2 TextureOffset { get; set; } = 0;
	[Property, MakeDirty] public Vector2 TextureSize { get; set; } = 512;
	[Property] public List<Vector2> Points { get; set; }
	[Property] public float ShardLifeTime { get; set; } = 1.0f;

	[StructLayout( LayoutKind.Sequential )]
	private struct Vertex
	{
		public Vector3 Position;
		public Vector3 Normal;
		public Vector2 TexCoord0;
		public Vector2 TexCoord1;
		public Vector3 Color;
		public Vector4 Tangent;

		public static readonly VertexAttribute[] Layout =
		{
			new( VertexAttributeType.Position, VertexAttributeFormat.Float32, 3 ),
			new( VertexAttributeType.Normal, VertexAttributeFormat.Float32, 3 ),
			new( VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 2, 0 ),
			new( VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 2, 1 ),
			new( VertexAttributeType.Color, VertexAttributeFormat.Float32, 3, 0 ),
			new( VertexAttributeType.Tangent, VertexAttributeFormat.Float32, 4 ),
		};
	}

	protected override void DrawGizmos()
	{
		foreach ( var point in Points )
		{
			Gizmo.Draw.LineSphere( new Vector3( point.x, point.y, 0 ), 4 );
		}

		Gizmo.Draw.Color = Color.Red;

		foreach ( var shard in Shards.Values )
		{
			if ( !shard.IsValid() )
				continue;

			var body = shard.PhysicsBody;
			if ( !body.IsValid() )
				continue;

			Gizmo.Draw.LineSphere( Transform.World.ToLocal( body.Transform ).Position, 1 );

			foreach ( var point in shard.Points )
			{
				var p = Transform.World.PointToLocal( body.Transform.PointToWorld( new Vector3( point.x, point.y, 0 ) ) );
				Gizmo.Draw.LineSphere( p, 1 );
			}
		}
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		CreatePrimaryShard();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		DestroyShards();
	}

	protected override void OnDirty()
	{
		base.OnDirty();

		DestroyShards();
		CreatePrimaryShard();
	}

	protected override void OnValidate()
	{
		base.OnValidate();

		Surface ??= Surface.FindByName( "glass" );
		Material ??= Material.Load( "materials/glass.vmat" );
	}

	private void CreatePrimaryShard()
	{
		var points = IsPathClockwise( Points ) ? Points.Reverse<Vector2>().ToList() : Points.ToList();
		CreateShard( Transform.World, points );
	}

	private void DestroyShards()
	{
		foreach ( var shard in Shards.Values )
		{
			shard.Destroy();
		}

		Shards.Clear();
	}

	protected override void OnPreRender()
	{
		foreach ( var kv in Shards )
		{
			var shard = kv.Value;
			if ( !shard.IsValid() )
				continue;

			var body = shard.PhysicsBody;
			if ( !body.IsValid() )
				continue;

			if ( !shard.IsLoose )
			{
				body.Transform = Transform.World;
			}

			shard.SceneObject.Transform = body.Transform;

			if ( shard.IsLoose && shard.TimeCreated > ShardLifeTime )
			{
				ShardsToRemove.Add( kv.Key );
				shard.Destroy();
			}
		}

		foreach ( var shard in ShardsToRemove )
		{
			Shards.Remove( shard );
		}

		ShardsToRemove.Clear();
	}

	private Shard CreateShard( Transform transform, List<Vector2> points )
	{
		if ( CalculatePathArea( points ) < 4.0f )
			return null;

		var hull = new List<Vector3>();
		float halfThickness = Thickness * 0.5f;

		foreach ( var point in points )
		{
			hull.Add( new Vector3( point.x, point.y, halfThickness ) );
			hull.Add( new Vector3( point.x, point.y, -halfThickness ) );
		}

		var body = new PhysicsBody( Scene.PhysicsWorld );
		var shape = body.AddHullShape( Vector3.Zero, Rotation.Identity, hull );
		shape.Tags.SetFrom( GameObject.Tags );
		shape.Surface = Surface;

		body.SetComponentSource( this );
		body.EnableCollisionSounds = false;
		body.OnIntersectionStart += OnPhysicsTouchStart;
		body.BodyType = PhysicsBodyType.Keyframed;
		body.Transform = transform;

		var model = CreateModel( points );
		var sceneObject = new SceneObject( Scene.SceneWorld, model, transform );
		sceneObject.SetComponentSource( this );
		sceneObject.Tags.SetFrom( GameObject.Tags );
		sceneObject.Batchable = false;

		var shard = new Shard( sceneObject, shape, points.ToArray() );
		Shards.Add( shape, shard );

		return shard;
	}

	private void DestroyShard( Shard shard )
	{
		if ( shard is null )
			return;

		Shards.Remove( shard.PhysicsShape );
		shard.Destroy();
	}

	public void OnDamage( in DamageInfo damage )
	{
		var shape = damage.Shape;
		if ( !shape.IsValid() )
			return;

		if ( !Shards.TryGetValue( shape, out var shard ) )
			return;

		if ( !shard.IsValid() )
			return;

		var transform = shard.PhysicsBody.Transform;
		var position = transform.PointToLocal( damage.Position );
		ShatterLocalSpace( shard, position, 0 );
	}

	private void ShatterLocalSpace( Shard shard, Vector2 position, Vector3 impulse )
	{
		if ( !shard.IsValid() )
			return;

		if ( !shard.IsPointInside( position ) )
			return;

		var points = shard.Points.ToList();
		var transform = shard.PhysicsBody.Transform;
		var isLoose = shard.IsLoose;
		var area = shard.Area;

		DestroyShard( shard );

		if ( area < 4.0f )
			return;

		var shards = GenerateShatterShards( position, points, transform );
		foreach ( var newShard in shards )
		{
			if ( !newShard.IsValid() )
				continue;

			if ( newShard.Points == null || newShard.Points.Length == 0 )
				continue;

			if ( isLoose || !IsPathOnEdge( newShard.Points, Points, 0.1f ) )
			{
				var body = newShard.PhysicsBody;
				if ( body.IsValid() )
				{
					body.BodyType = PhysicsBodyType.Dynamic;
					body.ApplyImpulseAt( body.Transform.PointToWorld( position ), impulse );
				}

				newShard.IsLoose = true;
			}
		}
	}

	private void OnPhysicsTouchStart( PhysicsIntersection c )
	{

	}

	private static float DistanceToEdge( Vector2 point, Vector2 start, Vector2 end )
	{
		var delta = end - start;
		var length = delta.Length;
		var direction = delta / length;
		var closestPoint = start + Vector3.Dot( point - start, direction ).Clamp( 0, length ) * direction;
		return (point - closestPoint).Length;
	}

	private static bool IsPathOnEdge( IList<Vector2> path1, IList<Vector2> path2, float threshold )
	{
		foreach ( var point in path1 )
		{
			for ( int i = 0; i < path2.Count; i++ )
			{
				float dist = DistanceToEdge( point, path2[i], path2[(i + 1) % (path2.Count)] );

				if ( dist <= threshold )
				{
					return true;
				}
			}
		}

		return false;
	}

	private static float CalculatePathArea( IList<Vector2> points )
	{
		float area = 0;

		int vertexCount = points.Count;
		if ( vertexCount < 3 )
		{
			return 0;
		}
		else
		{
			var v1 = points[0];

			for ( int i = 1; i < vertexCount - 1; i++ )
			{
				var v2 = points[i];
				var v3 = points[i + 1];

				float x1 = v2.x - v1.x;
				float y1 = v2.y - v1.y;
				float x2 = v3.x - v1.x;
				float y2 = v3.y - v1.y;

				area += MathF.Abs( x1 * y2 - x2 * y1 );
			}

			area = MathF.Abs( area * 0.5f );
		}

		return area;
	}

	private struct ShatterSpoke
	{
		public Vector2 OuterPos;
		public Vector2 IntersectionPos;
		public int IntersectsEdgeIndex;
		public float Length;
	};

	private struct ShatterEdgeSegment
	{
		public ShatterEdgeSegment( Vector2 start, Vector2 end )
		{
			Start = start;
			End = end;
		}

		public Vector2 Start;
		public Vector2 End;
	};

	private List<Shard> GenerateShatterShards( Vector2 stressPosition, IList<Vector2> points, Transform transform )
	{
		var shards = new List<Shard>();
		var shatterType = ShatterTypes[1];

		var minX = points.Min( x => x.x );
		var minY = points.Min( x => x.y );
		var maxX = points.Max( x => x.x );
		var maxY = points.Max( x => x.y );

		var min = new Vector2( minX, minY );
		var max = new Vector2( maxX, maxY );

		float spokeLength = (max - min).LengthSquared;
		int numSpokes = Math.Max( 3, Game.Random.Int( shatterType.SpokesMin, shatterType.SpokesMax ) );
		var spokes = new List<ShatterSpoke>();

		float segmentRange = (MathF.PI * 2.0f) / numSpokes;
		float limitedRangeDeviation = Math.Min( segmentRange, (MathF.PI * 2.0f) * (1.0f / 3.0f) );

		for ( int i = 0; i < numSpokes; i++ )
		{
			float spokeRadians = (i * segmentRange) + (Game.Random.Float( limitedRangeDeviation * -0.5f, limitedRangeDeviation * 0.5f ) * 0.9f);

			var spoke = new ShatterSpoke
			{
				OuterPos = new Vector2( stressPosition.x + spokeLength * MathF.Cos( spokeRadians ), stressPosition.y + spokeLength * MathF.Sin( spokeRadians ) ),
				IntersectionPos = Vector2.Zero,
				IntersectsEdgeIndex = -1,
				Length = -1
			};

			spokes.Insert( 0, spoke );
		}

		var edgeSegments = new List<ShatterEdgeSegment>();

		for ( int i = 0; i < points.Count; i++ )
		{
			var v1 = points[i];
			var v2 = points[i < points.Count - 1 ? i + 1 : 0];

			edgeSegments.Add( new ShatterEdgeSegment( v1, v2 ) );
		}

		for ( int spokeIndex = 0; spokeIndex < spokes.Count; spokeIndex++ )
		{
			for ( int edgeIndex = 0; edgeIndex < edgeSegments.Count; edgeIndex++ )
			{
				if ( LineIntersect( edgeSegments[edgeIndex].Start, edgeSegments[edgeIndex].End, spokes[spokeIndex].OuterPos, stressPosition, out var point ) )
				{
					var spoke = spokes[spokeIndex];
					spoke.IntersectionPos = point;
					spoke.IntersectsEdgeIndex = edgeIndex;
					spoke.Length = Vector2.DistanceBetween( stressPosition, spoke.IntersectionPos );

					spokes[spokeIndex] = spoke;

					break;
				}
			}
		}

		var centerHoleVertices = new List<Vector2>();

		for ( int spokeIndex = 0; spokeIndex < spokes.Count; spokeIndex++ )
		{
			int nextSpokeIndex = spokeIndex < spokes.Count - 1 ? spokeIndex + 1 : 0;
			int currentEdgeIndex = spokes[spokeIndex].IntersectsEdgeIndex;
			int nextEdgeIndex = spokes[nextSpokeIndex].IntersectsEdgeIndex;

			if ( nextSpokeIndex < 0 || currentEdgeIndex < 0 || nextEdgeIndex < 0 )
				continue;

			if ( spokes[spokeIndex].Length < 0.5f && spokes[nextSpokeIndex].Length < 0.5f )
				continue;

			var subShard = new List<Vector2>
			{
				stressPosition,
				spokes[spokeIndex].IntersectionPos
			};

			if ( currentEdgeIndex == nextEdgeIndex )
			{
				subShard.Add( spokes[nextSpokeIndex].IntersectionPos );
			}
			else
			{
				for ( int i = 0; i < 32 && currentEdgeIndex != nextEdgeIndex; i++ )
				{
					subShard.Add( edgeSegments[currentEdgeIndex].End );

					currentEdgeIndex = currentEdgeIndex < edgeSegments.Count - 1 ? currentEdgeIndex + 1 : 0;
				}

				subShard.Add( spokes[nextSpokeIndex].IntersectionPos );
			}

			Assert.True( subShard.Count >= 3 );

			var tipPoint1 = Vector2.Lerp( subShard[0], subShard[1], Game.Random.Float( shatterType.TipScaleMin, shatterType.TipScaleMax ) );
			var tipPoint2 = Vector2.Lerp( subShard[0], subShard[^1], Game.Random.Float( shatterType.TipScaleMin, shatterType.TipScaleMax ) );

			centerHoleVertices.Add( Vector2.Lerp( tipPoint1, tipPoint2, 0.5f ) );

			if ( shatterType.TipSpawnChance > 0 && Game.Random.Float( 0, shatterType.TipSpawnChance ) < 1.0f )
			{
				var tipShard = new List<Vector2>
				{
					subShard[0],
					tipPoint1,
					tipPoint2
				};
				ScaleVerts( tipShard, shatterType.TipScale );
				shards.Add( CreateShard( transform, tipShard ) );
			}

			if ( shatterType.SecondTipSpawnChance > 0 && Game.Random.Float( 0, shatterType.SecondTipSpawnChance ) < 1.0f )
			{
				var secondTipPoint1 = Vector2.Lerp( tipPoint1, subShard[1], Game.Random.Float( 0.2f, 0.5f ) );
				var secondTopPoint2 = Vector2.Lerp( tipPoint2, subShard[^1], Game.Random.Float( 0.2f, 0.5f ) );

				var tipShard = new List<Vector2>
				{
					tipPoint1,
					secondTipPoint1,
					secondTopPoint2,
					tipPoint2
				};
				ScaleVerts( tipShard, shatterType.SecondShardScale );
				shards.Add( CreateShard( transform, tipShard ) );

				tipPoint1 = secondTipPoint1;
				tipPoint2 = secondTopPoint2;
			}

			subShard.RemoveAt( 0 );
			subShard.Insert( 0, tipPoint1 );
			subShard.Add( tipPoint2 );

			if ( (tipPoint1 - tipPoint2).LengthSquared > 9.0f )
			{
				var vecBetweenCorners = Vector2.Lerp( Vector2.Lerp( tipPoint1, tipPoint2, Game.Random.Float( 0.4f, 0.6f ) ), stressPosition, Game.Random.Float( 0.1f, 0.3f ) );
				subShard.Add( vecBetweenCorners );
			}

			ScaleVerts( subShard, shatterType.ShardScale );
			shards.Add( CreateShard( transform, subShard ) );
		}

		if ( shatterType.HasCenterChunk && centerHoleVertices.Count > 2 )
		{
			var pShardCenter = new List<Vector2>();

			foreach ( var vertex in centerHoleVertices )
			{
				pShardCenter.Add( vertex );
			}

			ScaleVerts( pShardCenter, shatterType.CenterChunkScale );
			shards.Add( CreateShard( transform, pShardCenter ) );
		}

		return shards;
	}

	private static void ScaleVerts( List<Vector2> points, float scale )
	{
		if ( scale <= 0.0f )
			return;

		var average = Vector2.Zero;
		var pointCount = points.Count;

		if ( pointCount > 0 )
		{
			foreach ( var point in points )
			{
				average += point;
			}

			average /= pointCount;
		}

		for ( int i = 0; i < points.Count; ++i )
		{
			points[i] = Vector2.Lerp( average, points[i], scale );
		}
	}

	public Model CreateModel( List<Vector2> points )
	{
		var renderData = new RenderData();
		renderData.Init( points.Count );

		var average = Vector2.Zero;
		var pointCount = points.Count;

		if ( pointCount > 0 )
		{
			foreach ( var point in points )
			{
				average += point;
			}

			average /= pointCount;
		}

		float halfThickness = Thickness * 0.5f;

		renderData.Vertices.Add( new Vector3( average.x, average.y, halfThickness ) );

		for ( var i = 0; i < renderData.FaceVertexCount - 1; i++ )
		{
			renderData.Vertices.Add( new Vector3( points[i].x, points[i].y, halfThickness ) );
		}

		renderData.Vertices.Add( new Vector3( average.x, average.y, -halfThickness ) );

		for ( var i = 0; i < renderData.FaceVertexCount - 1; i++ )
		{
			renderData.Vertices.Add( new Vector3( points[i].x, points[i].y, -halfThickness ) );
		}

		var modelBuilder = new ModelBuilder();
		modelBuilder.AddCollisionHull( renderData.Vertices.ToArray() );

		for ( var i = 0; i < renderData.EdgeQuadCount; i++ )
		{
			var next = (i < renderData.EdgeQuadCount - 1) ? i + 1 : 0;
			renderData.Vertices.Add( new Vector3( points[i].x, points[i].y, -halfThickness ) );
			renderData.Vertices.Add( new Vector3( points[next].x, points[next].y, -halfThickness ) );
			renderData.Vertices.Add( new Vector3( points[next].x, points[next].y, halfThickness ) );
			renderData.Vertices.Add( new Vector3( points[i].x, points[i].y, halfThickness ) );
		}

		renderData.EdgeVerticesStart = renderData.TotalShardVertices - renderData.EdgeVertexCount;

		Assert.AreEqual( renderData.Vertices.Count, renderData.TotalShardVertices );

		return modelBuilder.AddMesh( CreateMesh( renderData ) )
			.Create();
	}

	private static readonly Vector2[] EdgeUVs = new[]
	{
		new Vector2( 0.0f, 0.0f ),
		new Vector2( 0.0f, 0.01f ),
		new Vector2( 0.01f, 0.01f ),
		new Vector2( 0.01f, 0.0f )
	};

	private struct RenderData
	{
		public List<Vector3> Vertices;

		public int TotalShardVertices;
		public int TotalSharedIndices;
		public int EdgeVerticesStart;
		public int FaceVertexCount;
		public int FaceTriangleCount;
		public int FaceIndexCount;
		public int EdgeQuadCount;
		public int EdgeVertexCount;
		public int EdgeTriangleCount;
		public int EdgeIndexCount;

		public void Init( int numPanelVerts )
		{
			FaceVertexCount = numPanelVerts + 1;
			FaceTriangleCount = FaceVertexCount - 1;
			FaceIndexCount = FaceTriangleCount * 3;

			EdgeQuadCount = FaceVertexCount - 1;
			EdgeVertexCount = EdgeQuadCount * 4;
			EdgeTriangleCount = EdgeQuadCount * 2;
			EdgeIndexCount = EdgeTriangleCount * 3;

			TotalShardVertices = FaceVertexCount + FaceVertexCount + EdgeVertexCount;
			TotalSharedIndices = FaceIndexCount + FaceIndexCount + EdgeIndexCount;

			Vertices = new List<Vector3>( (FaceVertexCount * 2) + EdgeVertexCount );
		}
	};

	private Mesh CreateMesh( RenderData renderData )
	{
		var vertices = new Vertex[renderData.TotalShardVertices];
		var indices = new int[renderData.TotalSharedIndices];
		var bounds = new BBox();

		for ( var i = 0; i < renderData.TotalShardVertices; i++ )
		{
			vertices[i].Position = renderData.Vertices[i];
			bounds = bounds.AddPoint( vertices[i].Position );

			var vertexPos = new Vector3( renderData.Vertices[i].x, renderData.Vertices[i].y, 0 );
			var u = Vector3.Dot( TextureAxisU, vertexPos ) / TextureScale.x;
			var v = Vector3.Dot( TextureAxisV, vertexPos ) / TextureScale.y;

			u += TextureOffset.x;
			v += TextureOffset.y;

			u /= TextureSize.x;
			v /= TextureSize.y;

			var uv = new Vector2( u, v );

			vertices[i].TexCoord0 = uv;
			vertices[i].TexCoord1 = vertexPos;

			if ( i < renderData.EdgeVerticesStart )
			{
				vertices[i].Color = Vector3.Zero;
			}
			else
			{
				vertices[i].TexCoord0 += EdgeUVs[i % 4];
				vertices[i].TexCoord1 += EdgeUVs[i % 4];
				vertices[i].Color[0] = 1;
				vertices[i].Color[1] = 0;
				vertices[i].Color[2] = 0;
			}
		}

		ComputeTriangleNormalAndTangent( out var normalSideA, out var tangentSideA,
			vertices[1].Position, vertices[0].Position, vertices[2].Position,
			vertices[1].TexCoord1, vertices[0].TexCoord1, vertices[2].TexCoord1 );

		ComputeTriangleNormalAndTangent( out var normalSideB, out var tangentSideB,
			vertices[renderData.FaceVertexCount].Position, vertices[renderData.FaceVertexCount + 1].Position, vertices[renderData.FaceVertexCount + 2].Position,
			vertices[renderData.FaceVertexCount].TexCoord1, vertices[renderData.FaceVertexCount + 1].TexCoord1, vertices[renderData.FaceVertexCount + 2].TexCoord1 );

		for ( var i = 0; i < renderData.FaceTriangleCount; i++ )
		{
			var index = i * 3;
			var offset0 = i + 1;
			var offset1 = (i + 2 < renderData.FaceVertexCount) ? i + 2 : 1;
			var offset2 = 0;

			indices[index] = offset1;
			indices[index + 1] = offset0;
			indices[index + 2] = offset2;

			vertices[offset0].Normal = normalSideA;
			vertices[offset1].Normal = normalSideA;
			vertices[offset2].Normal = normalSideA;

			vertices[offset0].Tangent = tangentSideA;
			vertices[offset1].Tangent = tangentSideA;
			vertices[offset2].Tangent = tangentSideA;

			index += renderData.FaceIndexCount;
			offset0 += renderData.FaceVertexCount;
			offset1 += renderData.FaceVertexCount;
			offset2 = renderData.FaceVertexCount;

			indices[index] = offset0;
			indices[index + 1] = offset1;
			indices[index + 2] = offset2;

			vertices[offset0].Normal = normalSideB;
			vertices[offset1].Normal = normalSideB;
			vertices[offset2].Normal = normalSideB;

			vertices[offset0].Tangent = tangentSideB;
			vertices[offset1].Tangent = tangentSideB;
			vertices[offset2].Tangent = tangentSideB;
		}

		var edgeIndexOffset = renderData.TotalSharedIndices - renderData.EdgeIndexCount;
		for ( var i = 0; i < renderData.EdgeQuadCount; i++ )
		{
			var index = edgeIndexOffset + (i * 6);
			var vertexOffset = renderData.EdgeVerticesStart + (i * 4);

			indices[index] = vertexOffset + 2;
			indices[index + 1] = vertexOffset + 1;
			indices[index + 2] = vertexOffset;

			indices[index + 3] = vertexOffset + 3;
			indices[index + 4] = vertexOffset + 2;
			indices[index + 5] = vertexOffset;

			ComputeTriangleNormalAndTangent( out var faceNormal, out var faceTangent,
				vertices[vertexOffset + 2].Position, vertices[vertexOffset + 1].Position, vertices[vertexOffset].Position,
				vertices[vertexOffset + 2].TexCoord1, vertices[vertexOffset + 1].TexCoord1, vertices[vertexOffset].TexCoord1 );

			vertices[vertexOffset].Normal = faceNormal;
			vertices[vertexOffset + 1].Normal = faceNormal;
			vertices[vertexOffset + 2].Normal = faceNormal;
			vertices[vertexOffset + 3].Normal = faceNormal;

			vertices[vertexOffset].Tangent = faceTangent;
			vertices[vertexOffset + 1].Tangent = faceTangent;
			vertices[vertexOffset + 2].Tangent = faceTangent;
			vertices[vertexOffset + 3].Tangent = faceTangent;
		}

		var mesh = new Mesh( Material ?? Material.Load( "materials/glass.vmat" ) );
		mesh.CreateVertexBuffer<Vertex>( vertices.Length, Vertex.Layout, vertices );
		mesh.CreateIndexBuffer( indices.Length, indices );
		mesh.Bounds = bounds;

		return mesh;
	}

	private struct ShatterType
	{
		public int SpokesMin;
		public int SpokesMax;
		public float TipScaleMin;
		public float TipScaleMax;
		public float TipSpawnChance;
		public float TipScale;
		public float ShardScale;
		public float SecondTipSpawnChance;
		public float SecondShardScale;
		public bool HasCenterChunk;
		public float CenterChunkScale;
		public int ShardLimit;

		public ShatterType(
			int spokesMin,
			int spokesMax,
			float tipScaleMin,
			float tipScaleMax,
			float tipSpawnChance,
			float tipScale,
			float shardScale,
			float secondTipSpawnChance,
			float secondShardScale,
			bool hasCenterChunk,
			float centerChunkScale,
			int shardLimit )
		{
			SpokesMin = spokesMin;
			SpokesMax = spokesMax;
			TipScaleMin = tipScaleMin;
			TipScaleMax = tipScaleMax;
			TipSpawnChance = tipSpawnChance;
			TipScale = tipScale;
			ShardScale = shardScale;
			SecondTipSpawnChance = secondTipSpawnChance;
			SecondShardScale = secondShardScale;
			HasCenterChunk = hasCenterChunk;
			CenterChunkScale = centerChunkScale;
			ShardLimit = shardLimit;
		}
	};

	private static readonly ShatterType[] ShatterTypes = new[]
	{
		new ShatterType( 5,    10,     0.2f,   0.5f,   1.0f,   0.95f,  1.0f,   8.0f,    0.98f,     false,  0.0f,   4   ),
		new ShatterType( 8,    14,     0.1f,   0.3f,   3.0f,   0.95f,  1.0f,   16.0f,   0.98f,     false,  0.0f,   4   ),
		new ShatterType( 8,    10,     0.4f,   0.6f,   0.0f,   0.95f,  0.95f,  1.2f,    0.98f,     false,  0.0f,   2   ),
		new ShatterType( 20,   20,     0.7f,   0.99f,  3.0f,   0.95f,  1.0f,   16.0f,   0.98f,     false,  0.9f,   10  ),
	};

	private static Vector4 ComputeTangentForFace( Vector3 faceS, Vector3 faceT, Vector3 normal )
	{
		var leftHanded = Vector3.Dot( Vector3.Cross( faceS, faceT ), normal ) < 0.0f;
		var tangent = Vector4.Zero;

		if ( !leftHanded )
		{
			faceT = Vector3.Cross( normal, faceS );
			faceS = Vector3.Cross( faceT, normal );
			faceS = faceS.Normal;

			tangent.x = faceS[0];
			tangent.y = faceS[1];
			tangent.z = faceS[2];
			tangent.w = 1.0f;
		}
		else
		{
			faceT = Vector3.Cross( faceS, normal );
			faceS = Vector3.Cross( normal, faceT );
			faceS = faceS.Normal;

			tangent.x = faceS[0];
			tangent.y = faceS[1];
			tangent.z = faceS[2];
			tangent.w = -1.0f;
		}

		return tangent;
	}

	private static Vector3 ComputeTriangleNormal( Vector3 v1, Vector3 v2, Vector3 v3 )
	{
		var e1 = v2 - v1;
		var e2 = v3 - v1;

		return (Vector3.Cross( e1, e2 ).Normal + (Vector3.Random * 0.1f)).Normal;
	}

	private static void ComputeTriangleTangentSpace( Vector3 p0, Vector3 p1, Vector3 p2, Vector2 t0, Vector2 t1, Vector2 t2, out Vector3 s, out Vector3 t )
	{
		const float epsilon = 1e-12f;

		s = Vector3.Zero;
		t = Vector3.Zero;

		var edge0 = new Vector3( p1.x - p0.x, t1.x - t0.x, t1.y - t0.y );
		var edge1 = new Vector3( p2.x - p0.x, t2.x - t0.x, t2.y - t0.y );

		var cross = Vector3.Cross( edge0, edge1 );

		if ( MathF.Abs( cross.x ) > epsilon )
		{
			s.x += -cross.y / cross.x;
			t.x += -cross.z / cross.x;
		}

		edge0 = new Vector3( p1.y - p0.y, t1.x - t0.x, t1.y - t0.y );
		edge1 = new Vector3( p2.y - p0.y, t2.x - t0.x, t2.y - t0.y );

		cross = Vector3.Cross( edge0, edge1 );

		if ( MathF.Abs( cross.x ) > epsilon )
		{
			s.y += -cross.y / cross.x;
			t.y += -cross.z / cross.x;
		}

		edge0 = new Vector3( p1.z - p0.z, t1.x - t0.x, t1.y - t0.y );
		edge1 = new Vector3( p2.z - p0.z, t2.x - t0.x, t2.y - t0.y );

		cross = Vector3.Cross( edge0, edge1 );

		if ( MathF.Abs( cross.x ) > epsilon )
		{
			s.z += -cross.y / cross.x;
			t.z += -cross.z / cross.x;
		}

		s = s.Normal;
		t = t.Normal;
	}

	private static void ComputeTriangleNormalAndTangent( out Vector3 outNormal, out Vector4 outTangent, Vector3 v0, Vector3 v1, Vector3 v2, Vector2 uv0, Vector2 uv1, Vector2 uv2 )
	{
		outNormal = ComputeTriangleNormal( v0, v1, v2 );
		ComputeTriangleTangentSpace( v0, v1, v2, uv0, uv1, uv2, out var faceS, out var faceT );
		outTangent = ComputeTangentForFace( faceS, faceT, outNormal );
	}

	private static bool LineIntersect( Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection )
	{
		intersection = Vector2.Zero;

		float xD1, yD1, xD2, yD2, xD3, yD3;
		float dot, deg, length1, length2;
		float segmentLength1, segmentLength2;
		float ua, div;

		xD1 = p2.x - p1.x;
		xD2 = p4.x - p3.x;
		yD1 = p2.y - p1.y;
		yD2 = p4.y - p3.y;
		xD3 = p1.x - p3.x;
		yD3 = p1.y - p3.y;

		length1 = MathF.Sqrt( xD1 * xD1 + yD1 * yD1 );
		length2 = MathF.Sqrt( xD2 * xD2 + yD2 * yD2 );

		dot = (xD1 * xD2 + yD1 * yD2);
		deg = dot / (length1 * length2);

		if ( Math.Abs( deg ) == 1.0f )
		{
			return false;
		}

		div = yD2 * xD1 - xD2 * yD1;
		ua = (xD2 * yD3 - yD2 * xD3) / div;
		intersection.x = p1.x + ua * xD1;
		intersection.y = p1.y + ua * yD1;

		xD1 = intersection.x - p1.x;
		xD2 = intersection.x - p2.x;
		yD1 = intersection.y - p1.y;
		yD2 = intersection.y - p2.y;
		segmentLength1 = MathF.Sqrt( xD1 * xD1 + yD1 * yD1 ) + MathF.Sqrt( xD2 * xD2 + yD2 * yD2 );

		xD1 = intersection.x - p3.x;
		xD2 = intersection.x - p4.x;
		yD1 = intersection.y - p3.y;
		yD2 = intersection.y - p4.y;
		segmentLength2 = MathF.Sqrt( xD1 * xD1 + yD1 * yD1 ) + MathF.Sqrt( xD2 * xD2 + yD2 * yD2 );

		if ( MathF.Abs( length1 - segmentLength1 ) > 0.01f || MathF.Abs( length2 - segmentLength2 ) > 0.01f )
		{
			return false;
		}

		return true;
	}

	private static bool IsPathClockwise( IList<Vector2> points )
	{
		float area = 0;
		for ( int i = 0; i < points.Count; i++ )
		{
			int j = (i + 1) % points.Count;
			area += (points[j].x - points[i].x) * (points[j].y + points[i].y);
		}

		return area < 0;
	}
}
