using System.ComponentModel;
using Sandbox.Network;

namespace Sandbox;

[AttributeUsage( AttributeTargets.Method )]
[CodeGenerator( CodeGeneratorFlags.Instance | CodeGeneratorFlags.WrapMethod, "__rpc_Broadcast" )]
[CodeGenerator( CodeGeneratorFlags.Static | CodeGeneratorFlags.WrapMethod, "Sandbox.Rpc.WrapStaticMethod" )]
public class BroadcastAttribute : Attribute
{

}

[AttributeUsage( AttributeTargets.Method )]
[CodeGenerator( CodeGeneratorFlags.Instance | CodeGeneratorFlags.WrapMethod, "__rpc_Authority" )]
public class AuthorityAttribute : Attribute
{

}

public static class Rpc
{
	public static Connection Caller { get; private set; }
	public static Guid CallerId => Caller.Id;

	public static bool Calling { get; private set; }

	/// <summary>
	/// Called right before calling an RPC function.
	/// </summary>
	public static void PreCall()
	{
		if ( Calling )
		{
			Calling = false;
			return;
		}

		Caller = Connection.Local;
	}

	[EditorBrowsable( EditorBrowsableState.Never )]
	public static void WrapStaticMethod( WrappedMethod m, params object[] argumentList )
	{
		if ( !Calling && SceneNetworkSystem.Instance is not null )
		{
			var msg = new StaticRpcMsg();
			msg.MethodIdentity = m.MethodIdentity;
			msg.TypeIdentity = TypeLibrary.GetType( m.TypeName ).Identity;
			msg.Arguments = argumentList;

			SceneNetworkSystem.Instance.Broadcast( msg );
		}

		PreCall();
		m.Resume();
	}

	internal static void HandleIncoming( StaticRpcMsg message, Connection source )
	{
		var type = TypeLibrary.GetTypeByIdent( message.TypeIdentity );

		if ( type is null )
		{
			throw new( $"Unknown Static RPC type for method with identity '{message.MethodIdentity}'" );
		}

		var method = type.GetMethodByIdent( message.MethodIdentity );
		
		if ( method is null )
		{
			throw new( $"Unknown Static RPC method with identity '{message.MethodIdentity}' on {type}" );
		}
		
		Calling = true;
		var oldCaller = Caller;
		Caller = source;
		
		method.Invoke( null, message.Arguments );
		
		Caller = oldCaller;
	}

	internal static void HandleIncoming( ObjectMessageMsg message, Connection source )
	{
		if ( message.Guid == Guid.Empty )
		{
			Log.Warning( $"OnObjectMessage: Failed to call RPC with identity '{message.MethodIdentity}' for unknown object" );
			return;
		}

		var obj = global::GameManager.ActiveScene.Directory.FindByGuid( message.Guid );
		if ( obj is null )
		{
			Log.Warning( $"OnObjectMessage: Unknown object {message.Guid}" );
			return;
		}

		//
		// If we don't have a component, then we're calling a method on the GameObject itself
		//
		if ( string.IsNullOrEmpty( message.Component ) )
		{
			var typeDesc = TypeLibrary.GetType( typeof( GameObject ) );
			InvokeRpc( message, typeDesc, obj, source );
			return;
		}

		//
		// Find on component
		//
		var component = obj.Components.FirstOrDefault( x => x.GetType().Name == message.Component );
		if ( component  is null )
		{
			Log.Warning( $"OnObjectMessage: Unknown Component {message.Component}" );
			return;
		}

		{
			var typeDesc = TypeLibrary.GetType( component.GetType() );
			InvokeRpc( message, typeDesc, component, source );
		}
	}

	static void InvokeRpc( in ObjectMessageMsg message, in TypeDescription typeDesc, in object targetObject, in Connection source )
	{
		var method = typeDesc.GetMethodByIdent( message.MethodIdentity );
		
		if ( method == null )
		{
			throw new( $"Unknown RPC with identity '{message.MethodIdentity}' on {typeDesc.Name}" );
		}

		Calling = true;
		var oldCaller = Caller;
		Caller = source;

		method.Invoke( targetObject, message.Arguments );

		Caller = oldCaller;
	}
}
