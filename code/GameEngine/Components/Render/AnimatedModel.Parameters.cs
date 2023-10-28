using Sandbox;
using Sandbox.Diagnostics;
using System;

public sealed partial class AnimatedModelComponent
{

	public void Set( string v, Vector3 value ) => _sceneObject?.SetAnimParameter( v, value );
	public void Set( string v, int value ) => _sceneObject?.SetAnimParameter( v, value );
	public void Set( string v, float value ) => _sceneObject?.SetAnimParameter( v, value );
	public void Set( string v, bool value ) => _sceneObject?.SetAnimParameter( v, value );
	public void Set( string v, Rotation value ) => _sceneObject?.SetAnimParameter( v, value );
	//	public void Set( string v, Enum value ) => _sceneObject.SetAnimParameter( v, value );

	public bool GetBool( string v ) => _sceneObject.GetBool( v );
	public int GetInt( string v ) => _sceneObject.GetInt( v );
	public float GetFloat( string v ) => _sceneObject.GetFloat( v );
	public Vector3 GetVector( string v ) => _sceneObject.GetVector3( v );
	public Rotation GetRotation( string v ) => _sceneObject.GetRotation( v );

	/// <summary>
	/// Converts value to vector local to this entity's eyepos and passes it to SetAnimVector
	/// </summary>
	public void SetLookDirection( string name, Vector3 eyeDirectionWorld )
	{
		var delta = eyeDirectionWorld * Transform.Rotation.Inverse;
		Set( name, delta );
	}
}
