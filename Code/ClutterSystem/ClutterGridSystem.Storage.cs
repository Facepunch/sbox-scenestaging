using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Clutter;

public sealed partial class ClutterGridSystem
{
	/// <summary>
	/// Manages storage and serialization of painted clutter instances.
	/// </summary>
	private sealed class ClutterStorage : BinarySerializable
	{
		public record Instance( Vector3 Position, Rotation Rotation, float Scale = 1f );

		private Dictionary<string, List<Instance>> _instances = [];

		public ClutterStorage() { }

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
		public IReadOnlyDictionary<string, List<Instance>> GetAllInstances() => _instances;

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
				list = [];
				_instances[modelPath] = list;
			}

			list.AddRange( instances );
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

			return totalRemoved;
		}

		/// <summary>
		/// Clears all instances for a specific model.
		/// </summary>
		public bool ClearModel( string modelPath )
		{
			return _instances.Remove( modelPath );
		}

		/// <summary>
		/// Clears all instances.
		/// </summary>
		public void ClearAll()
		{
			_instances.Clear();
		}

		public override void Serialize( ref BlobWriter writer )
		{
			writer.Write( _instances.Count );

			foreach ( var (modelPath, instances) in _instances )
			{
				// Write model path
				writer.Stream.Write( modelPath );
				writer.Stream.Write( instances.Count );

				foreach ( var instance in instances )
				{
					// Position
					writer.Stream.Write( instance.Position.x );
					writer.Stream.Write( instance.Position.y );
					writer.Stream.Write( instance.Position.z );

					// Rotation
					writer.Stream.Write( instance.Rotation.x );
					writer.Stream.Write( instance.Rotation.y );
					writer.Stream.Write( instance.Rotation.z );
					writer.Stream.Write( instance.Rotation.w );

					// Scale
					writer.Stream.Write( instance.Scale );
				}
			}
		}

		public override void Deserialize( ref BlobReader reader )
		{
			// Read number of model paths
			var modelCount = reader.Read<int>();

			_instances.Clear();

			// Read each model path and its instances
			for ( int i = 0; i < modelCount; i++ )
			{
				var modelPath = reader.Stream.Read<string>();
				var instanceCount = reader.Stream.Read<int>();

				List<Instance> instanceList = [];

				// Read each instance
				for ( int j = 0; j < instanceCount; j++ )
				{
					// position
					var posX = reader.Stream.Read<float>();
					var posY = reader.Stream.Read<float>();
					var posZ = reader.Stream.Read<float>();
					var position = new Vector3( posX, posY, posZ );

					// rotation
					var rotX = reader.Stream.Read<float>();
					var rotY = reader.Stream.Read<float>();
					var rotZ = reader.Stream.Read<float>();
					var rotW = reader.Stream.Read<float>();
					var rotation = new Rotation( rotX, rotY, rotZ, rotW );

					// uniform scale
					var scale = reader.Stream.Read<float>();

					instanceList.Add( new( position, rotation, scale ) );
				}

				_instances[modelPath] = instanceList;
			}
		}
	}
}
