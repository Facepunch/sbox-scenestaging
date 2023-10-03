using Sandbox;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public static class SceneUtility
{
	/// <summary>
	/// Find all "Id" guids, and replace them with new Guids.
	/// This is used to make GameObject serializations unique, so when
	/// you duplicate stuff, it copies over uniquely and keeps associations.
	/// </summary>
	public static void MakeGameObjectsUnique( JsonObject json )
	{
		Dictionary<Guid, Guid> translate = new ();

		//
		// Find all guids with "Id" as their name. Add them to translate 
		// with a new target value.
		//
		WalkJsonValues( json, ( k, v ) =>
		{
			if ( k != "Id" ) return v;

			if ( v.TryGetValue<Guid>( out var guid ) )
			{
				translate[guid] = Guid.NewGuid();
			}		

			return v;
		} );

		//
		// Find every guid and translate them, but only if they're in our
		// guid dictionary.
		//
		WalkJsonValues( json, ( k, v ) =>
		{
			if ( !v.TryGetValue<Guid>( out var guid ) ) return v;
			if ( !translate.TryGetValue( guid, out var updatedGuid ) ) return v;

			return updatedGuid;
		} );
	}

	static JsonNode WalkJsonValues( JsonNode node, Func<string, JsonValue, JsonNode> onValue, string keyName = null )
	{
		Action deferred = default;

		if ( node is JsonObject jsonObject )
		{
			foreach( var entry in jsonObject )
			{
				var key = entry.Key;
				var newValue = WalkJsonValues( entry.Value, onValue, key );
				if ( newValue == entry.Value ) continue;

				deferred += () =>
				{
					jsonObject[key] = newValue;
				};				
			}
		}

		if ( node is JsonArray array )
		{
			foreach( var a in array )
			{
				WalkJsonValues( a, onValue );
			}
		}

		if ( node is JsonValue value )
		{
			return onValue( keyName, value );
		}

		deferred?.Invoke();
		return node;
	}



}
