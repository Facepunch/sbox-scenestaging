using Sandbox;
using System;
using System.Linq;
using static Sandbox.Prefab;

public static class PrefabSystem
{
	/// <summary>
	/// Create an entity from an entity prefab
	/// </summary>
	public static GameObject Spawn( Prefab self, Transform tx )
	{
		var e = self.Root;

		var go = Create( e, tx );

		Scene.Active.Register( go );

		return go;
	}

	static GameObject Create( Prefab.Entry e, Transform transform )
	{
		var targetType = TypeLibrary.GetType( e.Class );
		if ( targetType is null ) return null;

		var instance = new GameObject();

		instance.Name = e.GetValue( "_name", "Untitled Object" );
		instance.Transform = transform;

		foreach( var entry in e.Components )
		{
			CreateComponent( entry, instance );
		}

		foreach( var entry in e.Entities )
		{
			var localPosition = entry.GetValue( "_localposition", Vector3.Zero );
			var localRotation = entry.GetValue( "_localrotation", Angles.Zero ).ToRotation();
			var localScale = entry.GetValue( "_scale", 1.0f );

			var childTx = new Transform( localPosition, localRotation, localScale );

			var child = Create( entry, childTx );
			if ( child is null ) continue;

			child.Scene = Scene.Active;
			child.Parent = instance;
		}

		return instance;
	}

	private static void CreateComponent( Prefab.Entry entry, GameObject gameObject )
	{
		var targetType = TypeLibrary.GetType<GameObjectComponent>( entry.Class );
		if ( targetType is null )
		{
			Log.Warning( $"Unknown component '{entry.Class}'" );
			return;
		}

		var instance = targetType.Create<GameObjectComponent>();
		instance.Enabled = true;
		if ( instance == null )
		{
			Log.Warning( "Couldn't create GameObjectComponent!" );
			return;
		} 

		foreach ( var prop in targetType.Properties.Where( x =>x.HasAttribute<PropertyAttribute>() ) )
		{
			if ( !entry.Keys.TryGetValue( prop.Name, out var key ) ) continue;

			try
			{
				prop.SetValue( instance, Json.FromNode( key.JsonValue, prop.PropertyType ) );
			}
			catch ( System.Exception e )
			{
				Log.Warning( e, $"Error when deserializing {prop.Name}" );
			}
		}

		instance.GameObject = gameObject;
		gameObject.Components.Add( instance );
	}
}
