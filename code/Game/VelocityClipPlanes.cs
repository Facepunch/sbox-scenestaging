using System;
using System.Buffers;

namespace Sandbox;

/// <summary>
/// Used to store a list of planes that an object is going to hit, and then
/// remove velocity from them so the object can slide over the surface without
/// going through any of the planes.
/// </summary>
internal struct VelocityClipPlanes : IDisposable
{
	Vector3 OrginalVelocity;
	Vector3 BumpVelocity;
	Vector3[] Planes;

	/// <summary>
	/// Maximum number of planes that can be hit
	/// </summary>
	public int Max { get; private set; }

	/// <summary>
	/// Number of planes we're currently holding
	/// </summary>
	public int Count { get; private set; }

	public VelocityClipPlanes( Vector3 originalVelocity, int max = 5 )
	{
		Max = max;
		OrginalVelocity = originalVelocity;
		BumpVelocity = originalVelocity;
		Planes = ArrayPool<Vector3>.Shared.Rent( max );
		Count = 0;
	}

	/// <summary>
	/// Try to add this plane and restrain velocity to it (and its brothers)
	/// </summary>
	/// <returns>False if we ran out of room and should stop adding planes</returns>
	public bool TryAdd( Vector3 normal, ref Vector3 velocity, float bounce )
	{
		if ( Count == Max )
		{
			velocity = 0;
			return false;
		}

		Planes[Count++] = normal;

		//
		// if we only hit one plane then apply the bounce
		//
		if ( Count == 1 )
		{
			//BumpVelocity = velocity;
			BumpVelocity = ClipVelocity( BumpVelocity, normal, 1.0f + bounce );
			velocity = BumpVelocity;

			return true;
		}

		//
		// clip to all of the planes we've put in
		//
		velocity = BumpVelocity;
		if ( TryClip( ref velocity ) )
		{
			// Hit the floor and the wall, go along the join
			if ( Count == 2 )
			{
				var dir = Vector3.Cross( Planes[0], Planes[1] );
				velocity = dir.Normal * dir.Dot( velocity );
			}
			else
			{
				velocity = Vector3.Zero;
				return true;
			}
		}

		//
		// We're moving in the opposite direction to our
		// original intention so just stop right there.
		//
		if ( velocity.Dot( OrginalVelocity ) < 0 )
		{
			velocity = 0;
		}

		return true;
	}

	/// <summary>
	/// Try to clip our velocity to all the planes, so we're not travelling into them
	/// Returns true if we clipped properly
	/// </summary>
	bool TryClip( ref Vector3 velocity )
	{
		for ( int i = 0; i < Count; i++ )
		{
			velocity = ClipVelocity( BumpVelocity, Planes[i] );

			if ( MovingTowardsAnyPlane( velocity, i ) )
				return false;
		}

		return true;
	}

	/// <summary>
	/// Returns true if we're moving towards any of our planes (except for skip)
	/// </summary>
	bool MovingTowardsAnyPlane( Vector3 velocity, int iSkip )
	{
		for ( int j = 0; j < Count; j++ )
		{
			if ( j == iSkip ) continue;
			if ( velocity.Dot( Planes[j] ) < 0 ) return false;
		}

		return true;
	}

	/// <summary>
	/// Start a new bump. Clears planes and resets BumpVelocity
	/// </summary>
	public void StartBump( Vector3 velocity )
	{
		BumpVelocity = velocity;
		Count = 0;
	}

	/// <summary>
	/// Clip the velocity to the normal
	/// </summary>
	Vector3 ClipVelocity( Vector3 vel, Vector3 norm, float overbounce = 1.0f )
	{
		var backoff = Vector3.Dot( vel, norm ) * overbounce;
		var o = vel - (norm * backoff);

		// garry: I don't totally understand how we could still
		//		  be travelling towards the norm, but the hl2 code
		//		  does another check here, so we're going to too.
		var adjust = Vector3.Dot( o, norm );
		if ( adjust >= 1.0f ) return o;

		adjust = MathF.Min( adjust, -1.0f );
		o -= norm * adjust;

		return o;
	}

	public void Dispose()
	{
		ArrayPool<Vector3>.Shared.Return( Planes );
	}
}
