using Sandbox.Network;


namespace Sandbox;

[AttributeUsage( AttributeTargets.Method )]
[CodeGenerator( CodeGeneratorFlags.Instance | CodeGeneratorFlags.WrapMethod, "__rpc_Broadcast" )]
public class BroadcastAttribute : Attribute
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

	internal static void HandleIncoming( ObjectMessageMsg message, Connection source )
	{
		if ( message.Guid == Guid.Empty )
		{
			throw new NotImplementedException( "TODO: global static rpcs?!?!" );
		}

		// TODO - flag for "global rpc", for calling statics?
		// DispatchRpc( null, .. )

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

	private static void InvokeRpc( in ObjectMessageMsg message, in TypeDescription typeDesc, in object targetObject, in Connection source )
	{
		var method = typeDesc.GetMethod( message.MessageName );
		if ( method == null )
		{
			throw new System.Exception( $"Unknown RPC '{message.MessageName}' on {typeDesc.Name}" );
		}

		Calling = true;
		var oldCaller = Caller;
		Caller = source;

		method.Invoke( targetObject, message.Arguments );

		Caller = oldCaller;
	}
}
