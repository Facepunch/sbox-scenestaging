using Sandbox;
using Sandbox.Diagnostics;
using System;

public sealed partial class SkinnedModelRenderer
{

	public void Set( string v, Vector3 value ) => SceneModel?.SetAnimParameter( v, value );
	public void Set( string v, int value ) => SceneModel?.SetAnimParameter( v, value );
	public void Set( string v, float value ) => SceneModel?.SetAnimParameter( v, value );
	public void Set( string v, bool value ) => SceneModel?.SetAnimParameter( v, value );
	public void Set( string v, Rotation value ) => SceneModel?.SetAnimParameter( v, value );
	//	public void Set( string v, Enum value ) => _sceneObject.SetAnimParameter( v, value );

	public bool GetBool( string v ) => SceneModel?.GetBool( v ) ?? false;
	public int GetInt( string v ) => SceneModel?.GetInt( v ) ?? 0;
	public float GetFloat( string v ) => SceneModel?.GetFloat( v ) ?? 0.0f;
	public Vector3 GetVector( string v ) => SceneModel?.GetVector3( v ) ?? Vector3.Zero;
	public Rotation GetRotation( string v ) => SceneModel?.GetRotation( v ) ?? Rotation.Identity;

	/// <summary>
	/// Converts value to vector local to this entity's eyepos and passes it to SetAnimVector
	/// </summary>
	public void SetLookDirection( string name, Vector3 eyeDirectionWorld )
	{
		var delta = eyeDirectionWorld * Transform.Rotation.Inverse;
		Set( name, delta );
	}
}
