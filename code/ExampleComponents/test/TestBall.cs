using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

public enum CardSuit { Heart, Diamond, Spade, Club }
public enum CardHand { None, OnePair, TwoPair, ThreeOfKind, Straight, Flush, FullHouse, FourOfKind, StraightFlush, RoyalFlush }

public struct CardData
{
	public CardSuit suit;
	public int number;

	public CardData(CardSuit _suit, int _number)
	{
		suit = _suit;
		number = _number;
	}

	public string GetDisplayString()
	{
		return $"{GetNumberString()}{GetSuitString()}";
	}

	public string GetNumberString()
	{
		switch ( number )
		{
			case 1: default: return "A"; 
			case 2: return "2"; 
			case 3: return "3";
			case 4: return "4";
			case 5: return "5";
			case 6: return "6"; 
			case 7: return "7"; 
			case 8: return "8";
			case 9: return "9";
			case 10: return "10";
			case 11: return "J";
			case 12: return "Q";
			case 13: return "K";
		}
	}

	public string GetSuitString()
	{
		switch ( suit )
		{
			case CardSuit.Heart: default: return "♥";
			case CardSuit.Diamond: return "♦"; 
			case CardSuit.Spade: return "♠"; 
			case CardSuit.Club: return "♣"; 
		}
	}
}
public sealed class TestBall : Component, Component.ICollisionListener
{
	public TestGameManager Manager { get; set; }

	public CardData CardData { get; set; }

	public bool IsLocked { get; set; }
	public bool IsUnknown { get; set; }

	protected override void OnAwake()
	{
		base.OnAwake();

		GameObject.Tags.Add( "ball" );
	}

	public void Init(CardData data, bool isLocked, bool isUnknown)
	{
		CardData = data;
		IsLocked = isLocked;
		IsUnknown = isUnknown;
		RefreshMaterial();
	}

	void RefreshMaterial()
	{
		var renderer = Components.Get<ModelRenderer>();
		renderer.MaterialOverride = IsUnknown
			? Material.Load( $"test/textures/cards/unknown_2.vmat" )
			: Material.Load( $"test/textures/cards/{GetSuitString()}_{GetNumberString()}.vmat" );

		if ( IsLocked )
			renderer.Tint = new Color( 1f, 1f, 0f );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if(Transform.Position.z < -50f)
		{
			if(Manager.GameState == GameState.Playing)
			{
				var sfx = Sound.Play( "sparkle_explosion", Transform.Position );
				sfx.Pitch = Game.Random.Float( 1.05f, 1.25f );
			}

			Manager.BallDestroyedInGutter( this );
			GameObject.Destroy();
		}
	}

	public void DestroyClick()
	{
		if ( Manager.GameState != GameState.Playing )
			return;

		if(IsLocked )
		{
			var lockedSfx = Sound.Play( "locked", Transform.Position );
			return;
		}

		var bubbleSfx = Sound.Play( "bubble", Transform.Position );
		bubbleSfx.Pitch = Game.Random.Float( 0.75f, 0.9f );
		bubbleSfx.Volume = 1.2f;

		var slipSfx = Sound.Play( "slip", Transform.Position );
		slipSfx.Pitch = Game.Random.Float( 0.6f, 0.8f );

		Manager.BallDestroyedClick( this );
		GameObject.Destroy();
	}

	string GetSuitString()
	{
		switch(CardData.suit)
		{
			case CardSuit.Heart: default: return "heart";
			case CardSuit.Diamond: return "diamond";
			case CardSuit.Spade: return "spade";
			case CardSuit.Club: return "club";
		}
	}

	string GetNumberString()
	{
		switch ( CardData.number )
		{
			case 1: default: return "ace";
			case 2: return "2";
			case 3: return "3";
			case 4: return "4";
			case 5: return "5";
			case 6: return "6";
			case 7: return "7";
			case 8: return "8";
			case 9: return "9";
			case 10: return "10";
			case 11: return "jack";
			case 12: return "queen";
			case 13: return "king";
		}
	}

	public void OnCollisionStart( Collision other )
	{
		var speed = other.Contact.Speed.LengthSquared;

		if(speed > 20000f)
		{
			var hit = Sound.Play( "ppball", other.Contact.Point );
			hit.Volume = Utils.Map( speed, 20000f, 170000f, 0f, 1.4f );
		}
	}

	public void OnCollisionUpdate( Collision other )
	{
		
	}

	public void OnCollisionStop( CollisionStop other )
	{
		
	}
}
