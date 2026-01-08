using System;
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
	private sealed class ClutterStorage : BinarySerializable
	{
		private readonly Scene _scene;

		public record Instance( Vector3 Position, Rotation Rotation, float Scale = 1f );

		private Dictionary<string, List<Instance>> _instances = [];
		private bool HasChanged = false;

		public ClutterStorage( Scene scene )
		{
			_scene = scene;
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

		public override void Serialize( BinaryWriter writer )
		{
			writer.Write( _instances.Count );

			foreach ( var (modelPath, instances) in _instances )
			{
				// Write model path
				writer.Write( modelPath );
				writer.Write( instances.Count );

				foreach ( var instance in instances )
				{
					// Position
					writer.Write( instance.Position.x );
					writer.Write( instance.Position.y );
					writer.Write( instance.Position.z );

					// Rotation
					writer.Write( instance.Rotation.x );
					writer.Write( instance.Rotation.y );
					writer.Write( instance.Rotation.z );
					writer.Write( instance.Rotation.w );

					// Scale
					writer.Write( instance.Scale );
				}
			}
		}

		public override void Deserialize( BinaryReader reader )
		{
			// Read number of model paths
			var modelCount = reader.ReadInt32();

			_instances.Clear();

			// Read each model path and its instances
			for ( int i = 0; i < modelCount; i++ )
			{
				var modelPath = reader.ReadString();
				var instanceCount = reader.ReadInt32();

				List<Instance> instanceList = [];

				// Read each instance
				for ( int j = 0; j < instanceCount; j++ )
				{
					// position
					var posX = reader.ReadSingle();
					var posY = reader.ReadSingle();
					var posZ = reader.ReadSingle();
					var position = new Vector3( posX, posY, posZ );

					// rotation
					var rotX = reader.ReadSingle();
					var rotY = reader.ReadSingle();
					var rotZ = reader.ReadSingle();
					var rotW = reader.ReadSingle();
					var rotation = new Rotation( rotX, rotY, rotZ, rotW );

					// uniform scale
					var scale = reader.ReadSingle();

					instanceList.Add( new ( position, rotation, scale ) );
				}

				_instances[modelPath] = instanceList;
			}

			HasChanged = false;
		}
	}
}
