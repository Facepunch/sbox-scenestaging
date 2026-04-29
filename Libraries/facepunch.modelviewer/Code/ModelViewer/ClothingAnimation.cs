using Sandbox;
using System;
using System.Collections.Generic;

public sealed class ClothingAnimation : Component
{
	[Property] public SkinnedModelRenderer Target { get; set; }

	class AnimationsL
	{
		public string Name;
	}

	List<AnimationsL> _animationGroup { get;set; } = new();

	bool _grounded = false;

	int _holdType = 0;

	protected override void OnUpdate()
	{

		if ( Input.Pressed( "Slot1" ) )
		{
			
			_grounded = !_grounded;
			Target.Set( "b_grounded", _grounded );
			
		}
		
		if ( Input.Pressed( "Slot2" ) )
		{
			_holdType = _holdType + 1;
					
			if ( _holdType > 5 )
			{
				_holdType = 0;
			}
			
			Target.Set( "holdtype", _holdType );
		}

		/*
		if ( Target is not null )
			{
				if ( Target.Model.AnimationCount > 0 )
				{

					for ( int i = 0; i < Target.Model.AnimationCount; i++ )
					{
					//Log.Info( Target.Model.GetAnimationName( i ) );
					_animationGroup.Add( new AnimationsL { Name = Target.Model.GetAnimationName( i ) } );
				}
				}
			}
		*/
	}
	protected override void OnStart()
	{
		base.OnStart();



		//

	}
	protected override void OnAwake()
	{
		base.OnAwake();
		
	}
}
