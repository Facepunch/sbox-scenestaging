using System;
using Sandbox.Diagnostics;
using Sandbox.Internal;
using Sandbox.Utility;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sandbox.Sdf;

[AttributeUsage( AttributeTargets.Method )]
public sealed class RegisterSdfTypesAttribute : Attribute
{
}

public delegate T SdfReader<T>( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<T>> sdfTypes )
	where T : ISdf<T>;

public delegate T SdfReader<TBase, T>( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<TBase>> sdfTypes )
	where TBase : ISdf<TBase> where T : TBase;

public interface ISdf<T>
	where T : ISdf<T>
{
	// ReSharper disable StaticMemberInGenericType
#pragma warning disable SB3000
	internal static List<(TypeDescription Type, SdfReader<T> Reader)> RegisteredTypes { get; } = new();
	private static bool _sTypesRegistered;
	// ReSharper enable StaticMemberInGenericType
#pragma warning restore SB3000

	internal static void EnsureTypesRegistered()
	{
		if ( _sTypesRegistered ) return;

		_sTypesRegistered = true;

		foreach ( var (method, _) in
		         GlobalGameNamespace.TypeLibrary.GetMethodsWithAttribute<RegisterSdfTypesAttribute>() )
		{
			method.Invoke( null );
		}

		RegisteredTypes.Sort( ( a, b ) => string.CompareOrdinal( a.Type.FullName, b.Type.FullName ) );
	}

	void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes );

	public static void RegisterType<TSdf>( SdfReader<T, TSdf> readRaw )
		where TSdf : T
	{
		RegisteredTypes.Add( (GlobalGameNamespace.TypeLibrary.GetType( typeof(TSdf) ),
			( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<T>> sdfTypes ) =>
				readRaw( ref reader, sdfTypes )) );
	}

	public void Write( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
	{
		EnsureTypesRegistered();

		var type = GlobalGameNamespace.TypeLibrary.GetType( GetType() );

		if ( !sdfTypes.TryGetValue( type, out var typeIndex ) )
		{
			typeIndex = RegisteredTypes.FindIndex( x => x.Type == type );

			if ( typeIndex == -1 )
			{
				throw new NotImplementedException( $"Unable to serialize SDF type {type.FullName}" );
			}

			sdfTypes[type] = typeIndex;
		}

		writer.Write( typeIndex );
		WriteRaw( ref writer, sdfTypes );
	}

	public static T Read( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<T>> sdfTypes )
	{
		var typeIndex = reader.Read<int>();
		var sdfReader = sdfTypes[typeIndex];

		return sdfReader( ref reader, sdfTypes );
	}
}

internal interface ISdfWorld
{
	int ClearCount { get; }
	int ModificationCount { get; }
	int Dimensions { get; }

	int Write( ref ByteStream msg, int prevModifications );
	bool Read( ref ByteStream msg );
}

public enum Operator
{
	Add,
	Subtract
}

public record struct Modification<TResource, TSdf>( TSdf Sdf, TResource Resource, Operator Operator )
	where TResource : SdfResource<TResource>
	where TSdf : ISdf<TSdf>;

public record struct ChunkModification<TSdf>( TSdf Sdf, Operator Operator )
	where TSdf : ISdf<TSdf>;

/// <summary>
/// Base type for entities representing a set of volumes / layers containing geometry generated from
/// signed distance fields.
/// </summary>
/// <typeparam name="TWorld">Non-abstract world type</typeparam>
/// <typeparam name="TChunk">Non-abstract chunk type</typeparam>
/// <typeparam name="TResource">Volume / layer resource</typeparam>
/// <typeparam name="TChunkKey">Integer coordinates used to index a chunk</typeparam>
/// <typeparam name="TArray">Type of <see cref="SdfArray{TSdf}"/> used to contain samples</typeparam>
/// <typeparam name="TSdf">Interface for SDF shapes used to make modifications</typeparam>
public abstract partial class SdfWorld<TWorld, TChunk, TResource, TChunkKey, TArray, TSdf> : Component, Component.ExecuteInEditor, ISdfWorld
	where TWorld : SdfWorld<TWorld, TChunk, TResource, TChunkKey, TArray, TSdf>
	where TChunk : SdfChunk<TWorld, TChunk, TResource, TChunkKey, TArray, TSdf>, new()
	where TResource : SdfResource<TResource>
	where TChunkKey : struct
	where TArray : SdfArray<TSdf>, new()
	where TSdf : ISdf<TSdf>
{
	private List<Modification<TResource, TSdf>> Modifications { get; } = new();

	private ConcurrentQueue<TChunk> UpdatedChunkQueue { get; } = new();

	private bool _receivingModifications;
	private float _opacity = 1f;

	/// <summary>
	/// Spacial dimensions. 2 for <see cref="Sdf2DWorld"/>, 3 for <see cref="Sdf3DWorld"/>.
	/// </summary>
	public abstract int Dimensions { get; }

	/// <summary>
	/// How many times this world has been cleared.
	/// </summary>
	public int ClearCount { get; private set; }

	/// <summary>
	/// How many modifications have been applied since the last time this world was cleared.
	/// </summary>
	public int ModificationCount => Modifications.Count;

	internal bool IsDestroying { get; private set; }

	private PhysicsBody PhysicsBody { get; set; }

	public float Opacity
	{
		get => _opacity;
		set
		{
			value = Math.Clamp( value, 0f, 1f );

			if ( _opacity == value ) return;

			_opacity = value;

			for ( var i = AllChunks.Count - 1; i >= 0; --i )
			{
				AllChunks[i].Opacity = value;
			}
		}
	}

	protected override void OnDestroy()
	{
		IsDestroying = true;

		base.OnDestroy();
	}

	private class Layer
	{
		public int LastChangeCount { get; set; }
		public Dictionary<TChunkKey, TChunk> Chunks { get; } = new();
		public HashSet<TChunk> NeedsMeshUpdate { get; } = new();
		public Task UpdateMeshTask { get; set; } = System.Threading.Tasks.Task.CompletedTask;
	}

	private Dictionary<TResource, Layer> Layers { get; } = new();
	private List<TChunk> AllChunks { get; } = new();

	private Task _lastModificationTask = System.Threading.Tasks.Task.CompletedTask;

	public bool NeedsMeshUpdate => UpdatedChunkQueue.Count > 0 || Layers.Values.Any( x => x.NeedsMeshUpdate.Count > 0 || !x.UpdateMeshTask.IsCompleted );

	protected override void OnUpdate()
	{
		UpdateTransform();
		ProcessUpdatedChunkQueue();

		foreach ( var (resource, layer) in Layers )
		{
			if ( resource.ChangeCount != layer.LastChangeCount )
			{
				layer.LastChangeCount = resource.ChangeCount;

				foreach ( var chunk in layer.Chunks.Values.ToArray() )
				{
					layer.NeedsMeshUpdate.Add( chunk );
				}
			}

			if ( layer.NeedsMeshUpdate.Count > 0 && layer.UpdateMeshTask.IsCompleted )
			{
				DispatchUpdateMesh( layer );
			}
		}

		if ( IsProxy || !Network.Active )
			return;

		foreach ( var conn in Connection.All.Where( c => c != Connection.Host && c.IsActive ) )
			SendModifications( conn );
	}

	public void UpdateTransform()
	{
		if ( PhysicsBody is { } body )
		{
			body.Transform = Transform.World;
		}

		for ( var i = AllChunks.Count - 1; i >= 0; --i )
		{
			AllChunks[i].UpdateTransform();
		}
	}

	public void Write( Stream stream )
	{
		throw new NotImplementedException();
	}

	public void Read( Stream stream )
	{
		throw new NotImplementedException();
	}

	private const int MaxModificationsPerMessage = 256;

	// ReSharper disable StaticMemberInGenericType
#pragma warning disable SB3000
	private static readonly Dictionary<TypeDescription, int> NetWrite_TypeIndices = new();
	private static readonly Dictionary<int, SdfReader<TSdf>> NetRead_TypeReaders = new();
	// ReSharper enable StaticMemberInGenericType
#pragma warning restore SB3000

	public int Write( ref ByteStream msg, int prevModifications )
	{
		var count = Math.Min( MaxModificationsPerMessage, ModificationCount - prevModifications );

		msg.Write( ClearCount );
		msg.Write( prevModifications );
		msg.Write( count );
		msg.Write( ModificationCount );

		WriteRange( ref msg, prevModifications, count, NetWrite_TypeIndices );

		return count;
	}

	private void WriteRange( ref ByteStream writer, int from, int count, Dictionary<TypeDescription, int> sdfTypes )
	{
		for ( var i = 0; i < count; ++i )
		{
			var modification = Modifications[from + i];

			writer.Write( (byte)modification.Operator );
			writer.Write( modification.Resource.ResourceId );
			modification.Sdf.Write( ref writer, sdfTypes );
		}
	}

	public bool Read( ref ByteStream msg )
	{
		var clearCount = msg.Read<int>();
		var prevCount = msg.Read<int>();
		var msgCount = msg.Read<int>();
		var totalCount = msg.Read<int>();

		using var clientMods = AllowClientModifications();

		if ( clearCount < ClearCount )
		{
			// Outdated
			return true;
		}

		if ( clearCount > ClearCount )
		{
			ClearCount = clearCount;
			_ = ClearAsync();

			Assert.AreEqual( 0, Modifications.Count );
		}

		if ( prevCount != Modifications.Count )
		{
			return false;
		}

		ISdf<TSdf>.EnsureTypesRegistered();

		NetRead_TypeReaders.Clear();

		var index = 0;
		foreach ( var (_, reader) in ISdf<TSdf>.RegisteredTypes )
		{
			NetRead_TypeReaders[index++] = reader;
		}

		ReadRange( ref msg, msgCount, NetRead_TypeReaders );

		return true;
	}

	private void ReadRange( ref ByteStream reader, int count, IReadOnlyDictionary<int, SdfReader<TSdf>> sdfTypes )
	{
		for ( var i = 0; i < count; ++i )
		{
			var op = (Operator)reader.Read<byte>();
			var resId = reader.Read<int>();
			var res = ResourceLibrary.Get<TResource>( resId );

			var sdf = ISdf<TSdf>.Read( ref reader, sdfTypes );

			var modification = new Modification<TResource, TSdf>( sdf, res, op );

			switch ( modification.Operator )
			{
				case Operator.Add:
					_ = AddAsync( modification.Sdf, modification.Resource );
					break;

				case Operator.Subtract:
					_ = SubtractAsync( modification.Sdf, modification.Resource );
					break;
			}
		}
	}

	private IDisposable AllowClientModifications()
	{
		_receivingModifications = true;

		return new DisposeAction( () => _receivingModifications = false );
	}

	[Obsolete( $"Please use {nameof(ClearAsync)}" )]
	public void Clear()
	{
		_ = ClearAsync();
	}

	/// <summary>
	/// Removes all layers / volumes, making this equivalent to a brand new empty world.
	/// </summary>
	public async Task ClearAsync()
	{
		if ( !IsDestroying )
		{
			AssertCanModify();
		}

		await GameTask.MainThread();

		Modifications.Clear();

		if ( !IsProxy || !Network.Active )
		{
			++ClearCount;
		}

		Task task;

		lock ( this )
		{
			task = _lastModificationTask = ClearImpl();
		}

		await task;
	}

	private async Task ClearImpl()
	{
		ThreadSafe.AssertIsMainThread();

		var lastTask = _lastModificationTask;

		if ( !lastTask.IsCompleted )
		{
			await lastTask;
		}

		await GameTask.WhenAll( Layers.Values.Select( x => x.UpdateMeshTask ) );
		await GameTask.MainThread();
		await GameTask.WhenAll( Layers.Values.SelectMany( x => x.Chunks.Values ).Select( async x =>
		{
			await x.ClearAsync( false );
			UpdatedChunkQueue.Enqueue( x );
		} ) );
	}

	[Obsolete( $"Please use {nameof(ClearAsync)}" )]
	public void Clear( TResource resource )
	{
		_ = ClearAsync( resource );
	}

	/// <summary>
	/// Removes the given layer or volume.
	/// </summary>
	/// <param name="resource">Layer or volume to clear</param>
	public Task ClearAsync( TResource resource )
	{
		throw new NotImplementedException();
	}

	private void AddAffectedChunks( HashSet<(TResource, TChunkKey)> outSet,
		IEnumerable<Modification<TResource, TSdf>> modifications )
	{
		foreach ( var modification in modifications )
		{
			var quality = modification.Resource.Quality;

			foreach ( var chunkKey in GetAffectedChunks( modification.Sdf, quality ) )
			{
				outSet.Add( (modification.Resource, chunkKey) );
			}
		}
	}

	public async Task SetModificationsAsync(
		IEnumerable<Modification<TResource, TSdf>> modifications,
		IEnumerable<Modification<TResource, TSdf>> toRebuild = null )
	{
		var chunksToRebuild = new HashSet<(TResource Resource, TChunkKey ChunkKey)>();

		AddAffectedChunks( chunksToRebuild, toRebuild ?? Modifications );

		Modifications.Clear();
		Modifications.AddRange( modifications );

		if ( toRebuild == null )
		{
			AddAffectedChunks( chunksToRebuild, Modifications );
		}

		var count = Modifications.Count;
		var resources = Modifications.Select( x => x.Resource )
			.Distinct()
			.ToArray();

		await GameTask.MainThread();

		foreach ( var resource in resources )
		{
			EnsureLayerExists( resource );
		}

		await GameTask.WhenAll( chunksToRebuild.Select( async x =>
		{
			await GameTask.WorkerThread();

			var quality = x.Resource.Quality;
			var chunkModifications = Modifications
				.Take( count )
				.Where( y => y.Resource == x.Resource && AffectsChunk( y.Sdf, quality, x.ChunkKey ) )
				.SkipWhile( y => y.Operator == Operator.Subtract )
				.Select( y => new ChunkModification<TSdf>( y.Sdf, y.Operator ) )
				.ToArray();

			var chunk = GetChunk( x.Resource, x.ChunkKey );

			if ( chunk is null )
			{
				if ( chunkModifications.Length == 0 )
				{
					return;
				}

				await GameTask.MainThread();
				chunk = GetOrCreateChunk( x.Resource, x.ChunkKey );
				await GameTask.WorkerThread();
			}

			await chunk.RebuildAsync( chunkModifications );

			UpdatedChunkQueue.Enqueue( chunk );

			await GameTask.MainThread();

			ChunkMeshUpdated( chunk, false );
		} ) );
	}

	private void EnsureLayerExists( TResource resource )
	{
		ThreadSafe.AssertIsMainThread();

		if ( !Layers.ContainsKey( resource ) )
		{
			Layers.Add( resource, new Layer() );
		}
	}

	[Obsolete( $"Please use {nameof(AddAsync)}" )]
	public bool Add<T>( in T sdf, TResource resource )
		where T : TSdf
	{
		_ = AddAsync( in sdf, resource );
		return false;
	}

	/// <summary>
	/// Add a shape to the given layer or volume.
	/// </summary>
	/// <typeparam name="T">SDF type</typeparam>
	/// <param name="sdf">Shape to add</param>
	/// <param name="resource">Layer or volume to add to</param>
	/// <returns>True if any geometry was modified</returns>
	public Task AddAsync<T>( in T sdf, TResource resource )
		where T : TSdf
	{
		EnsureLayerExists( resource );

		Modifications.Add( new Modification<TResource, TSdf>( sdf, resource, Operator.Add ) );

		return ModifyChunksAsync( sdf, resource, true, ( chunk, sdf ) => chunk.AddAsync( sdf ) );
	}

	[Obsolete( $"Please use {nameof(SubtractAsync)}" )]
	public bool Subtract<T>( in T sdf, TResource resource )
		where T : TSdf
	{
		_ = SubtractAsync( in sdf, resource );
		return false;
	}

	/// <summary>
	/// Subtract a shape from the given layer or volume.
	/// </summary>
	/// <typeparam name="T">SDF type</typeparam>
	/// <param name="sdf">Shape to subtract</param>
	/// <param name="resource">Layer or volume to subtract from</param>
	/// <returns>True if any geometry was modified</returns>
	public Task SubtractAsync<T>( in T sdf, TResource resource )
		where T : TSdf
	{
		EnsureLayerExists( resource );

		Modifications.Add( new Modification<TResource, TSdf>( sdf, resource, Operator.Subtract ) );

		return ModifyChunksAsync( sdf, resource, false, ( chunk, sdf ) => chunk.SubtractAsync( sdf ) );
	}

	[Obsolete( $"Please use {nameof(SubtractAsync)}" )]
	public bool Subtract<T>( in T sdf )
		where T : TSdf
	{
		_ = SubtractAsync( sdf );
		return false;
	}

	/// <summary>
	/// Subtract a shape from all layers.
	/// </summary>
	/// <typeparam name="T">SDF type</typeparam>
	/// <param name="sdf">Shape to subtract</param>
	/// <returns>True if any geometry was modified</returns>
	public async Task SubtractAsync<T>( T sdf )
		where T : TSdf
	{
		var tasks = new List<Task>();

		foreach ( var material in Layers.Keys )
			tasks.Add( SubtractAsync( sdf, material ) );

		await GameTask.WhenAll( tasks );
	}

	internal TChunk GetChunk( TResource resource, TChunkKey key )
	{
		if ( IsDestroying ) return null;

		return Layers.TryGetValue( resource, out var layerData )
		       && layerData.Chunks.TryGetValue( key, out var chunk )
			? chunk
			: null;
	}

	private TChunk GetOrCreateChunk( TResource resource, TChunkKey key )
	{
		if ( IsDestroying ) return null;

		if ( !Layers.TryGetValue( resource, out var layerData ) )
		{
			throw new Exception( "Layer doesn't exist!" );
		}

		if ( layerData.Chunks.TryGetValue( key, out var chunk ) )
		{
			return chunk;
		}

		chunk = Components.Create<TChunk>();
		layerData.Chunks[key] = chunk;
		chunk.Init( (TWorld)this, resource, key );

		AllChunks.Add( chunk );

		return chunk;
	}

	private void AssertCanModify()
	{
		Assert.True( !IsProxy || _receivingModifications,
			"Can only modify host-created SDF Worlds on the host." );
	}

	internal void ChunkMeshUpdated( TChunk chunk, bool removed )
	{
		foreach ( var (key, value) in Layers )
		{
			if ( key.ReferencedTextures == null ) continue;

			if ( key == chunk.Resource ) continue;

			foreach ( var layerTexture in key.ReferencedTextures )
			{
				if ( layerTexture.Source != chunk.Resource ) continue;

				if ( value.Chunks.TryGetValue( chunk.Key, out var matching ) )
					matching.UpdateLayerTexture( chunk.Resource, removed ? null : chunk );
			}
		}
	}

	/// <summary>
	/// Disable to not generate any collision meshes.
	/// </summary>
	[Property]
	public bool HasPhysics { get; set; } = true;

	internal PhysicsShape AddMeshShape( List<Vector3> vertices, List<int> indices )
	{
		if ( PhysicsBody is null )
		{
			PhysicsBody = new PhysicsBody( Scene.PhysicsWorld )
			{
				BodyType = PhysicsBodyType.Static,
				Transform = Transform.World
			};

			PhysicsBody.SetComponentSource( this );
		}

		return PhysicsBody.AddMeshShape( vertices, indices );
	}

	/// <summary>
	/// Implements getting the indices of chunks within the bounds of the given SDF.
	/// </summary>
	/// <typeparam name="T">SDF type.</typeparam>
	/// <param name="sdf">SDF to check the bounds of.</param>
	/// <param name="quality">Quality setting that affects chunk size.</param>
	/// <returns>Indices of possible affected chunks.</returns>
	protected abstract IEnumerable<TChunkKey> GetAffectedChunks<T>( T sdf, WorldQuality quality )
		where T : TSdf;

	protected abstract bool AffectsChunk<T>( T sdf, WorldQuality quality, TChunkKey chunkKey )
		where T : TSdf;

	private async Task ModifyChunksAsync<T>( T sdf, TResource resource, bool createChunks,
		Func<TChunk, T, Task<bool>> func )
		where T : TSdf
	{
		AssertCanModify();

		if ( resource == null ) throw new ArgumentNullException( nameof(resource) );

		Task task;

		lock ( this )
		{
			_lastModificationTask = task = ModifyChunksAsyncImpl( sdf, resource, createChunks, func );
		}

		await task;
	}

	private async Task ModifyChunksAsyncImpl<T>( T sdf, TResource resource, bool createChunks,
		Func<TChunk, T, Task<bool>> func )
		where T : TSdf
	{
		var prevTask = _lastModificationTask;

		await GameTask.WorkerThread();

		if ( !prevTask.IsCompleted )
		{
			await prevTask;
		}

		var affectedChunks = GetAffectedChunks( sdf, resource.Quality )
			.ToArray();

		if ( createChunks )
		{
			await GameTask.MainThread();

			foreach ( var key in affectedChunks )
			{
				GetOrCreateChunk( resource, key );
			}

			await GameTask.WorkerThread();
		}

		try
		{
			var tasks = affectedChunks
				.Select( x => GetChunk( resource, x ) )
				.Where( x => x is not null )
				.Select( x => (Chunk: x, Task: func( x, sdf )) )
				.ToArray();

			var result = await GameTask.WhenAll( tasks.Select( x => x.Task ) );

			for ( var i = 0; i < tasks.Length; ++i )
			{
				if ( result[i] )
				{
					UpdatedChunkQueue.Enqueue( tasks[i].Chunk );
				}
			}
		}
		catch ( Exception e )
		{
			Log.Error( e );
		}
	}

	private void ProcessUpdatedChunkQueue()
	{
		ThreadSafe.AssertIsMainThread();

		while ( UpdatedChunkQueue.TryDequeue( out var chunk ) )
		{
			if ( !chunk.IsValid )
			{
				continue;
			}

			if ( !Layers.TryGetValue( chunk.Resource, out var layer ) )
			{
				continue;
			}

			layer.NeedsMeshUpdate.Add( chunk );
		}
	}

	private void DispatchUpdateMesh( Layer layer )
	{
		ThreadSafe.AssertIsMainThread();

		if ( layer.NeedsMeshUpdate.Count == 0 )
		{
			return;
		}

		if ( layer.UpdateMeshTask.IsCompleted )
		{
			layer.UpdateMeshTask = UpdateMesh( layer );
		}
	}

	private async Task UpdateMesh( Layer layer )
	{
		ThreadSafe.AssertIsMainThread();

		var tasks = layer.NeedsMeshUpdate.Select( x => x.UpdateMesh() ).ToArray();

		layer.NeedsMeshUpdate.Clear();

		await GameTask.WhenAll( tasks );
	}
}
