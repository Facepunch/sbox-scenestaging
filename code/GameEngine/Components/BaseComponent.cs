using System;

public abstract partial class BaseComponent
{
	public Scene Scene => GameObject.Scene;
	public GameTransform Transform => GameObject.Transform;

	public GameObject GameObject { get; internal set; }

	public GameObjectFlags Flags { get; set; } = GameObjectFlags.None;


	bool _isInitialized = false;

	/// <summary>
	/// Called to call Awake, once, at startup.
	/// </summary>
	internal void InitializeComponent()
	{
		if ( _isInitialized ) return;
		if ( GameObject is null ) return;
		if ( !GameObject.Active ) return;

		_isInitialized = true;
		ExceptionWrap( "Awake", OnAwake );
	}


	/// <summary>
	/// Internal functions to be called when the object wakes up
	/// </summary>
	Action onPostDeserialize;

	bool _enabledState;
	bool _enabled = false;

	public bool Enabled
	{
		get => _enabled;

		set
		{
			if ( _enabled == value ) return;

			_enabled = value;

			SceneUtility.ActivateComponent( this );
		}
	}

	public bool Active
	{
		get => _enabledState;
	}


	private bool ShouldExecute => Scene is not null && (!Scene.IsEditor || this is ExecuteInEditor);

	public virtual void DrawGizmos() { }

	/// <summary>
	/// Called once per component
	/// </summary>
	public virtual void OnAwake() { }

	/// <summary>
	/// Called after Awake or whenever the component switches to being enabled (because a gameobject heirachy active change, or the component changed)
	/// </summary>
	public virtual void OnEnabled() { }

	/// <summary>
	/// Called once before the first Update - when enabled.
	/// </summary>
	public virtual void OnStart() { }

	public virtual void OnDisabled() { }

	/// <summary>
	/// Called once, when the component or gameobject is destroyed
	/// </summary>
	public virtual void OnDestroy() { }

	protected virtual void OnPreRender() { }
	internal void PreRender()
	{
		OnPreRender();
	}

	bool _startCalled;

	internal virtual void InternalUpdate()
	{
		if ( !Enabled ) return;
		if ( !ShouldExecute ) return;

		if ( !_startCalled )
		{
			_startCalled = true;
			ExceptionWrap( "Start", OnStart );
		}

		ExceptionWrap( "Update", Update );
	}

	public Action OnComponentActivated { get; set; }
	public Action OnComponentDeactivated { get; set; }

	internal void PostDeserialize()
	{
		onPostDeserialize?.Invoke();
		onPostDeserialize = null;
	}

	internal void UpdateEnabledStatus()
	{
		var state = _enabled && Scene is not null && GameObject is not null && GameObject.Active;
		if ( state == _enabledState ) return;

		_enabledState = state;

		if ( _enabledState )
		{
			InitializeComponent();

			if ( ShouldExecute )
			{
				ExceptionWrap( "OnEnabled", OnEnabled );

				OnComponentActivated?.Invoke();
			}
		}
		else
		{
			if ( ShouldExecute )
			{
				ExceptionWrap( "OnDisabled", OnDisabled );

				OnComponentDeactivated?.Invoke();
			}
		}
	}

	public void Destroy()
	{
		ExceptionWrap( "OnDestroy", OnDestroy );

		if ( _enabledState )
		{
			_enabledState = false;
			_enabled = false;

			if ( ShouldExecute )
			{
				ExceptionWrap( "OnDisabled", OnDisabled );

				OnComponentDeactivated?.Invoke();
			}
		}

		GameObject.Components.RemoveAll( x => x == this );
	}

	public virtual void Reset()
	{

	}

	void ExceptionWrap( string name, Action a )
	{
		try
		{
			a();
		}
		catch ( System.Exception e )
		{
			Log.Error( e, $"Exception when calling '{name}' on {this}" );
		}
	}

	public virtual void Update()
	{

	}

	public virtual void FixedUpdate()
	{

	}

	public virtual void EditorUpdate()
	{

	}

	/// <summary>
	/// Called immediately after deserializing, and when a property is changed in the editor.
	/// </summary>
	public virtual void OnValidate()
	{

	}

	internal virtual void OnValidateInternal()
	{
		OnValidate();
	}


	/// <summary>
	/// Called when something on the component has been edited
	/// </summary>
	public void EditLog( string name, object source )
	{
		ExceptionWrap( "OnValidate", OnValidate );

		GameObject.EditLog( name, source );
	}

	public bool rpcFromNetwork;

	public void RpcWrap( Action resume, string methodName )
	{

	}

	internal void RpcWrap<T1>( Action resume, string methodName, T1 t1 )
	{
		Log.Info( $"Calling \"{methodName}\" RPC {rpcFromNetwork}" );

		if ( !rpcFromNetwork )
		{

			// Tell everyone to call this
			GameObject.CallRpc( this, methodName, t1 );
		}

		rpcFromNetwork = false;

		// we want to call this
		resume();
	}

	internal void RpcWrap<T1, T2>( Action resume, string methodName, T1 t1, T2 t2 )
	{
		Log.Info( $"Calling \"{methodName}\" RPC {rpcFromNetwork}" );

		if ( !rpcFromNetwork )
		{

			// Tell everyone to call this
			GameObject.CallRpc( this, methodName, t1, t2 );
		}

		rpcFromNetwork = false;

		// we want to call this
		resume();
	}

	internal void RpcWrap<T1, T2, T3>( Action resume, string methodName, T1 t1, T2 t2, T3 t3 )
	{
		Log.Info( $"Calling \"{methodName}\" RPC {rpcFromNetwork}" );

		if ( !rpcFromNetwork )
		{

			// Tell everyone to call this
			GameObject.CallRpc( this, methodName, t1, t2, t3 );
		}

		rpcFromNetwork = false;

		// we want to call this
		resume();
	}

	internal void RpcWrap<T1, T2, T3, T4>( Action resume, string methodName, T1 t1, T2 t2, T3 t3, T4 t4 )
	{
		Log.Info( $"Calling \"{methodName}\" RPC {rpcFromNetwork}" );

		if ( !rpcFromNetwork )
		{

			// Tell everyone to call this
			GameObject.CallRpc( this, methodName, t1, t2, t3, t4 );
		}

		rpcFromNetwork = false;

		// we want to call this
		resume();
	}

	internal void RpcWrap<T1, T2, T3, T4, T5>( Action resume, string methodName, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5 )
	{
		Log.Info( $"Calling \"{methodName}\" RPC {rpcFromNetwork}" );

		if ( !rpcFromNetwork )
		{

			// Tell everyone to call this
			GameObject.CallRpc( this, methodName, t1, t2, t3, t4, t5 );
		}

		rpcFromNetwork = false;

		// we want to call this
		resume();
	}
}
