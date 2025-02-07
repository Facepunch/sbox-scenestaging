using System.Collections.Generic;

namespace Sandbox.Sdf;

public partial class SdfWorld<TWorld, TChunk, TResource, TChunkKey, TArray, TSdf> : Component
{
	private Dictionary<Connection, ConnectionState> ConnectionStates { get; } = new();

	private record struct ConnectionState( int clearCount, int modificationCount, TimeSince lastMessage );

	private const float HeartbeatPeriod = 2f;

	private void SendModifications( Connection conn )
	{
		if ( !ConnectionStates.TryGetValue( conn, out var state ) )
			state = new ConnectionState( 0, 0, 0f );

		if ( state.clearCount != ClearCount )
			state = state with { clearCount = ClearCount, modificationCount = 0 };
		else if ( state.modificationCount >= ModificationCount && state.lastMessage < HeartbeatPeriod )
			return;

		state = state with { lastMessage = 0f };

		var byteStream = ByteStream.Create( 512 );
		var count = Write( ref byteStream, state.modificationCount );

		ConnectionStates[conn] = state with { modificationCount = state.modificationCount + count };

		using ( Rpc.FilterInclude( conn ) )
		{
			Rpc_SendModifications( byteStream.ToArray() );
		}

		byteStream.Dispose();
	}

	[Broadcast]
	private void Rpc_RequestMissing( int clearCount, int modificationCount )
	{
		var conn = Rpc.Caller;

		if ( !ConnectionStates.TryGetValue( conn, out var state ) )
		{
			Log.Info( $"Can't find connection state for {conn.DisplayName}" );
			return;
		}

		if (state.clearCount != clearCount || state.modificationCount <= modificationCount)
			return;
		
		ConnectionStates[conn] = state with { modificationCount = modificationCount };
	}

	private TimeSince _notifiedMissingModifications = float.PositiveInfinity;

	[Broadcast]
	private void Rpc_SendModifications( byte[] bytes )
	{
		var byteStream = ByteStream.CreateReader( bytes );
		if ( Read( ref byteStream ) )
		{
			_notifiedMissingModifications = float.PositiveInfinity;
			return;
		}

		if ( _notifiedMissingModifications >= 0.5f )
		{
			_notifiedMissingModifications = 0f;

			using ( Rpc.FilterInclude( Rpc.Caller ) )
			{
				Rpc_RequestMissing( ClearCount, ModificationCount );
			}
		}
	}
}
