using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Editor.ActionJigs
{
	public record UserDataProperty<T>
		where T : IEquatable<T>
	{
		public JsonObject UserData { get; }
		public string Key { get; }
		public T DefaultValue { get; }

		private T _value;

		public UserDataProperty( JsonObject userData, string key, T defaultValue = default )
		{
			UserData = userData;
			Key = key;
			DefaultValue = defaultValue;

			_value = defaultValue;

			if ( !userData.TryGetPropertyValue( Key, out var node ) )
			{
				return;
			}

			try
			{
				_value = node.Deserialize<T>();
			}
			catch ( Exception e )
			{
				Log.Error( e );
			}
		}

		public T Value
		{
			get => _value;
			set
			{
				if ( (_value ?? DefaultValue).Equals( value ) )
				{
					return;
				}

				_value = value;
				UserData[Key] = JsonSerializer.SerializeToNode( value );
			}
		}
	}
}
