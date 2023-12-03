using Sandbox;
using System;
using System.Threading;

public abstract partial class BaseComponent
{
	public Scene Scene => GameObject.Scene;
	public GameTransform Transform => GameObject.Transform;

	public GameObject GameObject { get; internal set; }

	public GameObjectFlags Flags { get; set; } = GameObjectFlags.None;

	/// <summary>
	/// Allow creating tasks that are automatically cancelled when the GameObject is destroyed.
	/// </summary>
	protected TaskSource Task => GameObject.Task;

	/// <summary>
	/// Allow creating tasks that are automatically cancelled when the GameObject is destroyed.
	/// </summary>
	public ComponentList Components => GameObject.Components;


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

		CallbackBatch.Add( CommonCallback.Awake, OnAwake, this, "OnAwake" );
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

			UpdateEnabledStatus();
		}
	}

	public bool Active
	{
		get => _enabledState;
	}


	private bool ShouldExecute => Scene is not null && (!Scene.IsEditor || this is ExecuteInEditor);


	/// <summary>
	/// Called once per component
	/// </summary>
	protected virtual void OnAwake() { }

	/// <summary>
	/// Called after Awake or whenever the component switches to being enabled (because a gameobject heirachy active change, or the component changed)
	/// </summary>
	protected virtual void OnEnabled() { }



	protected virtual void OnDisabled() { }

	/// <summary>
	/// Called once, when the component or gameobject is destroyed
	/// </summary>
	protected virtual void OnDestroy() { }

	protected virtual void OnPreRender() { }
	internal void PreRender()
	{
		OnPreRender();
	}



	public Action OnComponentEnabled { get; set; }
	public Action OnComponentDisabled { get; set; }

	internal void PostDeserialize()
	{
		onPostDeserialize?.Invoke();
		onPostDeserialize = null;
	}

	internal void UpdateEnabledStatus()
	{
		using var batch = CallbackBatch.StartGroup();

		var state = _enabled && Scene is not null && GameObject is not null && GameObject.Active;
		if ( state == _enabledState ) return;

		_enabledState = state;

		if ( _enabledState )
		{
			InitializeComponent();

			if ( ShouldExecute )
			{
				CallbackBatch.Add( CommonCallback.Enable, OnEnabled, this, "OnEnabled" );
				CallbackBatch.Add( CommonCallback.Enable, () => OnComponentEnabled?.Invoke(), this, "OnComponentEnabled" );
			}

			Scene.RegisterComponent( this );
		}
		else
		{
			if ( ShouldExecute )
			{
				CallbackBatch.Add( CommonCallback.Disable, OnDisabled, this, "OnDisabled" );
				CallbackBatch.Add( CommonCallback.Disable, () => OnComponentDisabled?.Invoke(), this, "OnComponentDisabled" );
			}

			Scene.UnregisterComponent( this );
		}
	}

	public void Destroy()
	{
		using var batch = CallbackBatch.StartGroup();

		GameObject.Components.OnDestroyedInternal( this );

		CallbackBatch.Add( CommonCallback.Destroy, OnDestroy, this, "OnDestroy" );

		if ( _enabledState )
		{
			_enabledState = false;
			_enabled = false;
			Scene.UnregisterComponent( this );

			if ( ShouldExecute )
			{
				CallbackBatch.Add( CommonCallback.Disable, OnDisabled, this, "OnDisabled" );
				CallbackBatch.Add( CommonCallback.Disable, () => OnComponentDisabled?.Invoke(), this, "OnComponentDisabled" );
			}
		}
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


	/// <summary>
	/// Called immediately after deserializing, and when a property is changed in the editor.
	/// </summary>
	protected virtual void OnValidate()
	{

	}

	internal virtual void OnValidateInternal()
	{
		CallbackBatch.Add( CommonCallback.Validate, OnValidate, this, "OnValidate" );
	}


	/// <summary>
	/// Called when something on the component has been edited
	/// </summary>
	public void EditLog( string name, object source )
	{
		ExceptionWrap( "OnValidate", OnValidate );

		GameObject.EditLog( name, source );
	}



	/// <summary>
	/// When tags have been updated
	/// </summary>
	protected virtual void OnTagsChannged()
	{

	}

	internal virtual void OnTagsUpdatedInternal()
	{
		OnTagsChannged();
	}

}
