using Sandbox;
using Sandbox.Physics;
using System;

public class GameTransform
{
	public GameObject GameObject { get; init; }
	public GameObject Parent => GameObject.Parent;

	public Action OnTransformChanged;

	public TransformProxy Proxy { get; set; }

	internal GameTransform( GameObject owner )
	{
		GameObject = owner;
		_local = Transform.Zero;
	}

	Transform _local;
	Transform _fixedLocal;

	/// <summary>
	/// The current local transform
	/// </summary>
	public Transform Local
	{
		get
		{
			if ( Proxy is not null )
			{
				return Proxy.GetLocalTransform();
			}

			if ( GameObject.Scene.IsFixedUpdate )
			{
				return _fixedLocal;
			}

			return _local;
		}

		set
		{
			if ( value.Position.IsNaN ) throw new System.ArgumentOutOfRangeException();

			if ( Proxy is not null )
			{
				Proxy.SetLocalTransform( value );
				return;
			}

			if ( Local == value )
				return;

			_local = value;
			_fixedLocal = value;

			TransformChanged();

			if ( GameObject.Scene.IsFixedUpdate )
			{
				LerpTo( _local, Time.Delta );
			}
		}
	}

	/// <summary>
	/// Our transform has changed, which means our children transforms changed too
	/// tell them all.
	/// </summary>
	void TransformChanged()
	{
		OnTransformChanged?.Invoke();
		GameObject.ForEachChild( "TransformChanged", true, ( c ) => c.Transform.TransformChanged() );
	}

	/// <summary>
	/// The current world transform
	/// </summary>
	public Transform World
	{
		get
		{
			if ( Parent is null ) return Local;
			if ( Parent is Scene ) return Local;

			if ( Proxy is not null )
			{
				return Proxy.GetWorldTransform();
			}

			return Parent.Transform.World.ToWorld( Local );
		}

		set
		{
			if ( value.Position.IsNaN ) throw new System.ArgumentOutOfRangeException();

			if ( Proxy is not null )
			{
				Proxy.SetWorldTransform( value );
				return;
			}

			if ( Parent is null || Parent is Scene )
			{
				Local = value;
				return;
			}

			Local = Parent.Transform.World.ToLocal( value );
		}
	}

	/// <summary>
	/// The position in world coordinates
	/// </summary>
	public Vector3 Position
	{
		get => World.Position;
		set
		{
			if ( value.IsNaN ) throw new System.ArgumentOutOfRangeException();

			World = World.WithPosition( value );
		}
	}

	/// <summary>
	/// The rotation in world coordinates
	/// </summary>
	public Rotation Rotation
	{
		get => World.Rotation;
		set => World = World.WithRotation( value );
	}

	/// <summary>
	/// The scale in world coordinates
	/// </summary>
	public Vector3 Scale
	{
		get => World.Scale;
		set => World = World.WithScale( value.x );
	}

	/// <summary>
	/// Position in local coordinates
	/// </summary>
	public Vector3 LocalPosition
	{
		get => Local.Position;
		set
		{
			Local = Local.WithPosition( value );
		}
	}

	/// <summary>
	/// Rotation in local coordinates
	/// </summary>
	public Rotation LocalRotation
	{
		get => Local.Rotation;
		set
		{
			Local = Local.WithRotation( value );
		}
	}

	/// <summary>
	/// Scale in local coordinates
	/// </summary>
	public Vector3 LocalScale
	{
		get => Local.Scale;
		set
		{
			Local = Local.WithScale( value.x );
		}
	}

	TransformInterpolate interp = new TransformInterpolate();

	public void LerpTo( in Transform target, float timeToTake )
	{
		interp.Add( Time.Now + timeToTake, target );
	}

	public void ClearLerp()
	{
		interp.Clear( Local );
	}

	internal void Update()
	{
		if ( interp.entries is null )
			return;

		interp.CullOlderThan( Time.Now - 1.0f );

		if ( interp.Query( Time.Now, ref _local ) )
		{
			// okay
		}
	}

	public void FDdd()
	{
		//
	}
}
