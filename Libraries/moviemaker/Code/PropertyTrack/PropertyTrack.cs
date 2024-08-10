using System;
using System.Text.Json.Nodes;

namespace Sandbox.MovieMaker.Tracks;

public class PropertyTrack : MovieTrack
{
	public GameObject GameObject { get; private set; }
	public Component Component { get; private set; }
	public string PropertyName { get; private set; }

	internal Func<object> ReadValue;
	internal Action<object> WriteValue;
	protected Type PropertyType;

	Guid goGuid;
	string goName;
	string goParentName;

	Guid compGuid;
	string compType;

	public void SetPropertyName( string name )
	{
		PropertyName = name;
	}

	private void InitializeFromGameObject( GameObject go, string propertyName )
	{
		GameObject = go;
		Component = default;
		PropertyName = propertyName;

		UpdateLookups();
	}

	private void InitializeFromComponent( Component cmp, string propertyName )
	{
		Component = cmp;
		GameObject = cmp.GameObject;
		PropertyName = propertyName;

		UpdateLookups();
	}

	void UpdateLookups()
	{
		if ( GameObject.IsValid() )
		{
			goGuid = GameObject.Id;
			goName = GameObject.Name;

			if ( GameObject.Parent.IsValid() && GameObject.Parent is not Scene )
			{
				goParentName = GameObject.Parent.Name;
			}
			else
			{
				goParentName = default;
			}
		}

		if ( Component.IsValid() )
		{
			compGuid = Component.Id;
			compType = Component.GetType().Name;
		}
	}

	public void InitProperty()
	{
		if ( Component is Component cmpj )
		{
			var type = TypeLibrary.GetType( Component.GetType() );
			WriteValue = ( v ) => type.SetValue( Component, PropertyName, v );
			ReadValue = () =>
			{
				return type.GetValue( Component, PropertyName );
			};

			PropertyType = type.GetProperty( PropertyName ).PropertyType;


			return;
		}

		if ( GameObject is GameObject obj )
		{
			var type = TypeLibrary.GetType<GameObject>();
			var txtype = TypeLibrary.GetType<GameTransform>();

			WriteValue = ( v ) => txtype.SetValue( (GameObject as GameObject).Transform, PropertyName, v );
			ReadValue = () => txtype.GetValue( (GameObject as GameObject).Transform, PropertyName );
			PropertyType = txtype.GetProperty( PropertyName ).PropertyType;

			return;
		}

		Log.Warning( "Couldn't do something" );
	}

	protected override JsonObject Serialize()
	{
		UpdateLookups();

		var o = new JsonObject();

		o["GameObject"] = Json.ToNode( GameObject );
		o["GameObjectGuid"] = goGuid;
		o["GameObjectName"] = goName;
		o["GameObjectParent"] = goParentName;

		o["Property"] = PropertyName;

		// we serialize extra data to help us resolve
		// the gameobject/component if something bad happens

		if ( !string.IsNullOrEmpty( compType ) )
		{
			o["Component"] = Json.ToNode( Component );
			o["ComponentGuid"] = compGuid;
			o["ComponentType"] = compType;
		}

		return o;
	}

	protected override void Deserialize( JsonObject obj )
	{
		GameObject = Json.FromNode<GameObject>( obj["GameObject"] ) ?? default;
		Component = Json.FromNode<Component>( obj["Component"] ) ?? default;

		goGuid = obj["GameObjectGuid"]?.GetValue<Guid>() ?? Guid.Empty;
		goName = obj["GameObjectName"]?.GetValue<string>();
		goParentName = obj["GameObjectParent"]?.GetValue<string>();

		PropertyName = obj["Property"]?.GetValue<string>();

		InitProperty();
	}

	public override void Play( float time )
	{

	}

	internal bool Matches( GameObject go, string property )
	{
		if ( GameObject != go ) return false;
		if ( PropertyName != property ) return false;

		return true;
	}

	internal bool Matches( Component component, string property )
	{
		if ( GameObject != component.GameObject ) return false;
		if ( Component != component ) return false;
		if ( PropertyName != property ) return false;

		return true;
	}

	internal static PropertyTrack CreateFor( GameObject go, string property )
	{
		PropertyTrack track = default;

		if ( property == "LocalScale" || property == "LocalPosition" )
		{
			track = new PropertyVector3Track();
		}

		if ( property == "LocalRotation" )
		{
			track = new PropertyRotationTrack();
		}

		track.InitializeFromGameObject( go, property );

		return track;
	}

	internal static PropertyTrack CreateFor( Component component, string property )
	{
		var p = TypeLibrary.GetType( component.GetType() );

		Type targetType = null;

		var prop = p.GetProperty( property );
		if ( prop is not null )
		{
			targetType = prop.PropertyType;
		}

		PropertyTrack track = default;

		if ( targetType == typeof( float ) ) track = new PropertyFloatTrack() { GameObject = component.GameObject, Component = component, PropertyName = property };
		else if ( targetType == typeof( Vector3 ) ) track = new PropertyVector3Track() { GameObject = component.GameObject, Component = component, PropertyName = property };
		else if ( targetType == typeof( Rotation ) ) track = new PropertyRotationTrack() { GameObject = component.GameObject, Component = component, PropertyName = property };
		else if ( targetType == typeof( Color ) ) track = new PropertyColorTrack() { GameObject = component.GameObject, Component = component, PropertyName = property };
		else track = new PropertyGenericTrack() { GameObject = component.GameObject, Component = component, PropertyName = property };

		track.InitializeFromComponent( component, property );

		return track;
	}


	/// <summary>
	/// Used when adding keyframes - read the current value from the property
	/// </summary>
	public virtual object ReadCurrentValue()
	{
		return ReadValue?.Invoke();
	}

	public record struct PropertyKeyframe( float time, object value ); // keyframes etc

	public virtual void WriteFrames( PropertyKeyframe[] frames )
	{

	}

	public virtual PropertyKeyframe[] ReadFrames()
	{
		return default;
	}

}
