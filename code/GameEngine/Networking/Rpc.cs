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
	public static void WrapStaticMethod( Action resume, string methodName, params object[] argumentList )
	{
		if ( !Calling && SceneNetworkSystem.Instance is not null )
		{
			var msg = new StaticRpcMsg();
			msg.MethodIndex = FindMethodIndex( methodName );
			msg.Arguments = argumentList;

			SceneNetworkSystem.Instance.Broadcast( msg );
		}

		PreCall();
		resume();
	}

	internal static void HandleIncoming( StaticRpcMsg message, Connection source )
	{
		var fullName = FindMethodName( message.MethodIndex );
		var split = fullName.Split( "." );
		var typeName = string.Join( ".", split[..^1] );
		var methodName = split[^1];
		var type = TypeLibrary.GetType( typeName );

		if ( type == null )
		{
			throw new( $"Unknown Static RPC type '{typeName}'" );
		}

		var method = type.Methods.FirstOrDefault( m => m.IsStatic && m.Name == methodName && m.Parameters.Length == message.Arguments.Length );
		
		if ( method == null )
		{
			throw new( $"Unknown Static RPC method '{methodName}' on {typeName}" );
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
			Log.Warning( $"OnObjectMessage: Failed to call RPC with index '{message.MethodIndex}' for unknown object" );
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
		var methodName = FindMethodName( message.MethodIndex );
		var method = typeDesc.GetMethod( methodName );
		
		if ( method == null )
		{
			throw new System.Exception( $"Unknown RPC '{methodName}' on {typeDesc.Name}" );
		}

		Calling = true;
		var oldCaller = Caller;
		Caller = source;

		method.Invoke( targetObject, message.Arguments );

		Caller = oldCaller;
	}

	/// <summary>
	/// Try to find a method name string from the supplied index.
	/// </summary>
	/// <param name="index"></param>
	internal static string FindMethodName( int index )
	{
		if ( !_hasIndexedMethodNames )
			IndexMethodNames();

		if ( !_indexToMethodName.TryGetValue( index, out var methodName ) )
		{
			throw new( $"Unknown Static RPC method with index '{index}'" );
		}

		return methodName;
	}

	/// <summary>
	/// Try to find an index from the supplied method name.
	/// </summary>
	/// <param name="methodName"></param>
	internal static int FindMethodIndex( string methodName )
	{
		if ( !_hasIndexedMethodNames )
			IndexMethodNames();

		if ( !_methodNameToIndex.TryGetValue( methodName, out var index ) )
		{
			throw new( $"Unindexed RPC method '{methodName}'" );
		}
		
		return index;
	}

	static Dictionary<string, int> _methodNameToIndex;
	static Dictionary<int, string> _indexToMethodName;
	static bool _hasIndexedMethodNames;

	[Event.Hotload]
	static void OnHotload()
	{
		IndexMethodNames();
	}

	static void IndexMethodNames()
	{
		_methodNameToIndex = new();
		_indexToMethodName = new();
		
		var currentIndex = 0;
		var staticMethods = TypeLibrary
			.GetMethodsWithAttribute<BroadcastAttribute>()
			.Select( ( m ) => $"{m.Method.TypeDescription.FullName}.{m.Method.Name}" )
			.Distinct()
			.ToList();
		
		var instanceMethods = TypeLibrary.GetTypes()
			.SelectMany( x => x.Members )
			.OfType<MethodDescription>()
			.Where( x => !x.IsStatic && ( x.Attributes.OfType<BroadcastAttribute>().Any() || x.Attributes.OfType<AuthorityAttribute>().Any() ) )
			.Select( ( m ) => m.Name );

		var methods = staticMethods
			.Concat( instanceMethods )
			.Distinct()
			.ToList();

		methods.Sort( ( a, b ) => string.Compare( a, b, StringComparison.Ordinal ) );

		foreach ( var methodName in methods )
		{
			_methodNameToIndex[methodName] = currentIndex;
			_indexToMethodName[currentIndex] = methodName;
			currentIndex++;
		}
		
		_hasIndexedMethodNames = true;
	}
}
