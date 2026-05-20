using System;
using System.Collections.Generic;
using Sandbox;

namespace Voxels.Physics;

internal sealed class VoxelCollisionMesh
{
	private readonly List<Vector3> _vertices = new();
	private readonly List<int> _indices = new();

	public void UpdateVertices( ReadOnlySpan<Vector3> vertices )
	{
		_vertices.Clear();
		_vertices.AddRange( vertices );
	}

	public void UpdateIndices( ReadOnlySpan<int> indices )
	{
		_indices.Clear();
		_indices.AddRange( indices );
	}

	public void UpdateShape( PhysicsBody physicsBody, ref PhysicsShape? physicsShape )
	{
		if ( !physicsShape.IsValid() )
		{
			physicsShape = physicsBody.AddMeshShape( _vertices, _indices );
		}
		else
		{
			physicsShape.UpdateMesh( _vertices, _indices );
		}
	}
}
