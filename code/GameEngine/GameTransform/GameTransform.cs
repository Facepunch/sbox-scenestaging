using System;

public class GameTransform
{
	public GameObject GameObject { get; init; }
	public GameObject Parent => GameObject.Parent;

	public Action<GameTransform> OnTransformChanged;

	internal GameTransform( GameObject owner )
	{
		GameObject = owner;
		_local = Transform.Zero;
	}

	Transform _local;

	/// <summary>
	/// The current local transform
	/// </summary>
	public Transform Local
	{
		get => _local;
		set
		{
			if ( value.Position.IsNaN ) throw new System.ArgumentOutOfRangeException();

			if ( _local == value )
				return;

			_local = value;
			OnTransformChanged?.Invoke( this );
		}
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

			return Parent.Transform.World.ToWorld( Local );
		}

		set
		{
			if ( value.Position.IsNaN ) throw new System.ArgumentOutOfRangeException();

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
		get => _local.Position;
		set
		{
			Local = _local.WithPosition( value );
		}
	}

	/// <summary>
	/// Rotation in local coordinates
	/// </summary>
	public Rotation LocalRotation
	{
		get => _local.Rotation;
		set
		{
			Local = _local.WithRotation( value );
		}
	}

	/// <summary>
	/// Scale in local coordinates
	/// </summary>
	public Vector3 LocalScale
	{
		get => _local.Scale;
		set
		{
			Local = _local.WithScale( value.x );
		}
	}
}
