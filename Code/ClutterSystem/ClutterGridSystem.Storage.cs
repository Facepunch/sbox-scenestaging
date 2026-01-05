using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Sandbox.Clutter;

public sealed partial class ClutterGridSystem
{
	/// <summary>
	/// Manages storage and serialization of painted clutter instances.
	/// </summary>
	private sealed class ClutterStorage
	{
		private const string METADATA_KEY = "ClutterStorage";
		private readonly Scene _scene;

		public record Instance( Vector3 Position, Rotation Rotation, float Scale = 1f );

		private Dictionary<string, List<Instance>> _instances = [];
		private bool HasChanged = false;

		public ClutterStorage( Scene scene )
		{
			_scene = scene;
			Load();
		}

		/// <summary>
		/// Gets the total number of instances across all models.
		/// </summary>
		public int TotalCount
		{
			get
			{
				var count = 0;
				foreach ( var list in _instances.Values )
					count += list.Count;
				return count;
			}
		}

		/// <summary>
		/// Gets all model paths that have instances.
		/// </summary>
		public IEnumerable<string> ModelPaths => _instances.Keys;

		/// <summary>
		/// Gets instances for a specific model path.
		/// </summary>
		public IReadOnlyList<Instance> GetInstances( string modelPath )
		{
			if ( _instances.TryGetValue( modelPath, out var list ) )
				return list;

			return [];
		}

		/// <summary>
		/// Gets all instances grouped by model path.
		/// </summary>
		public IReadOnlyDictionary<string, List<Instance>> GetAllInstances()
		{
			return _instances;
		}

		/// <summary>
		/// Adds a single instance for a model.
		/// </summary>
		public void AddInstance( string modelPath, Vector3 position, Rotation rotation, float scale = 1f )
		{
			if ( string.IsNullOrEmpty( modelPath ) ) return;

			if ( !_instances.TryGetValue( modelPath, out var list ) )
			{
				list = [];
				_instances[modelPath] = list;
			}

			list.Add( new Instance( position, rotation, scale ) );
			HasChanged = true;
		}

		/// <summary>
		/// Adds multiple instances for a model.
		/// </summary>
		public void AddInstances( string modelPath, IEnumerable<Instance> instances )
		{
			if ( string.IsNullOrEmpty( modelPath ) ) return;
			if ( instances == null ) return;

			if ( !_instances.TryGetValue( modelPath, out var list ) )
			{
				list = new();
				_instances[modelPath] = list;
			}

			list.AddRange( instances );
			HasChanged = true;
		}

		/// <summary>
		/// Erases all instances within a radius of a position.
		/// </summary>
		public int Erase( Vector3 position, float radius )
		{
			if ( _instances.Count == 0 ) return 0;

			var radiusSquared = radius * radius;
			var totalRemoved = 0;

			foreach ( var list in _instances.Values )
			{
				var removed = list.RemoveAll( i => i.Position.DistanceSquared( position ) <= radiusSquared );
				totalRemoved += removed;
			}

			if ( totalRemoved > 0 )
				HasChanged = true;

			return totalRemoved;
		}

		/// <summary>
		/// Clears all instances for a specific model.
		/// </summary>
		public bool ClearModel( string modelPath )
		{
			if ( _instances.Remove( modelPath ) )
			{
				HasChanged = true;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Clears all instances.
		/// </summary>
		public void ClearAll()
		{
			_instances.Clear();
			HasChanged = true;
		}

		/// <summary>
		/// Saves changes to Scene.Metadata if there are any.
		/// </summary>
		public void Save()
		{
			if ( !HasChanged ) return;

			// Remove empty model entries
			var emptyKeys = new List<string>();
			foreach ( var kvp in _instances )
			{
				if ( kvp.Value.Count == 0 )
					emptyKeys.Add( kvp.Key );
			}

			foreach ( var key in emptyKeys )
				_instances.Remove( key );

			// Save to metadata
			if ( _instances.Count == 0 )
				_scene.Metadata.Remove( METADATA_KEY );
			else
				_scene.Metadata[METADATA_KEY] = Pack( _instances );

			HasChanged = false;
		}

		/// <summary>
		/// Loads data from Scene.Metadata.
		/// </summary>
		public void Load()
		{
			_instances = LoadFromScene();
			HasChanged = false;
		}

		private Dictionary<string, List<Instance>> LoadFromScene()
		{
			if ( !_scene.Metadata.TryGetValue( METADATA_KEY, out var metadataValue ) )
				return [];

			return Unpack( metadataValue as string );
		}

		private static string Pack( Dictionary<string, List<Instance>> data )
		{
			var json = JsonSerializer.Serialize( data );
			var bytes = Encoding.UTF8.GetBytes( json );

			using var mem = new MemoryStream();
			using ( var zip = new GZipStream( mem, CompressionLevel.Optimal ) )
			{
				zip.Write( bytes );
			}

			var base64 = Convert.ToBase64String( mem.ToArray() );
			base64 = base64.Replace( '+', '-' );
			base64 = base64.Replace( '/', '_' );
			base64 = base64.TrimEnd( '=' );

			return base64;
		}

		private static Dictionary<string, List<Instance>> Unpack( string data )
		{
			if ( string.IsNullOrEmpty( data ) )
				return [];

			var base64 = data.Replace( '-', '+' ).Replace( '_', '/' );
			var padding = (4 - base64.Length % 4) % 4;
			base64 = base64.PadRight( base64.Length + padding, '=' );

			var compressed = Convert.FromBase64String( base64 );

			using var mem = new MemoryStream( compressed );
			using var zip = new GZipStream( mem, CompressionMode.Decompress );
			using var reader = new StreamReader( zip, Encoding.UTF8 );

			var json = reader.ReadToEnd();
			var result = JsonSerializer.Deserialize<Dictionary<string, List<Instance>>>( json );

			return result ?? new();
		}
	}
}
