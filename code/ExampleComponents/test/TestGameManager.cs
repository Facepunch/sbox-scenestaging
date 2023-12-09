global using System;
global using Sandbox;
global using System.Linq;
global using System.Threading.Tasks;
global using System.Collections.Generic;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

public enum CursorType { Pointer, Crosshair }
public enum GameState { Playing, Finished, WaitingForRestart }

public class TopScoreData
{
	public int Score { get; set; }
}

public sealed class TestGameManager : Component
{
	[Property] GameObject Ball { get; set; }
	[Property] GameObject GutterParticle { get; set; }
	[Property] GameObject ClickRemoveParticle { get; set; }

	private GameObject _ballContainer;

	private List<CardData> _cards;

	public TestCamPlayer TestCamPlayer { get; private set; }

	public CursorType CursorType { get; set; }

	public List<CardData> CollectedCards = new List<CardData>();
	public List<List<CardData>> FinishedSets = new List<List<CardData>>();
	public List<float> FinishedSetTimes = new List<float>();

	public TimeSince TimeSinceCollect { get; set; }
	public TimeSince TimeSinceFinishSet { get; set; }
	public TimeSince TimeSinceFinishGame { get; set; }
	public TimeSince TimeSinceNewSecond { get; set; }

	public GameState GameState { get; private set; }

	public float ElapsedTime { get; private set; }
	public const float GAME_TIME = 120f;
	private int _currSecond;
	public string GameFinishedMessage { get; set; }

	public float FinishGameTime { get; set; }
	public int FinishedNumHandsTallied { get; private set; }
	public TimeSince TimeSinceTally { get; set; }
	public int CurrTalliedScore { get; private set; }
	public int LastScoreTally { get; set; }
	public int TotalScore { get; private set; }
	public int TopScore { get; private set; }

	public int ExistingStatScore { get; private set; }

	private int _numLocked;
	private int _numUnknown;

	protected override void OnAwake()
	{
		base.OnAwake();

		_cards = new List<CardData>();

		TestCamPlayer = Scene.GetAllComponents<TestCamPlayer>().FirstOrDefault();
		TestCamPlayer.Manager = this;

		_ballContainer = Scene.GetAllObjects( true ).Where( x => x.Name == "BallContainer" ).FirstOrDefault();
		
		Restart();
		//SpawnBall( new Vector3( 0f, -210f, 1200f ) );
		//SpawnBall( new Vector3( 0f, -160f, 1200f ) );
		//SpawnBall( new Vector3( 0f, 210f, 1200f ) );
		//SpawnBall( new Vector3( 0f, 160f, 1200f ) );

		TopScoreData topScoreData = FileSystem.Data.ReadJson<TopScoreData>( "ballpoker_top_score.json" );

		if(topScoreData != null)
			TopScore = topScoreData.Score;

		_ = UpdatePlayerStats();

		//FileSystem.Data.WriteJson<TopScoreData>( "ballpoker_top_score.json", new TopScoreData() { Score = 0 } );
		//Sandbox.Services.Stats.SetValue( "score", 0 );
	}

	public static Sandbox.Services.Stats.PlayerStats StartStats;

	async Task UpdatePlayerStats()
	{
		StartStats = Sandbox.Services.Stats.LocalPlayer.Copy();
		await StartStats.Refresh();

		ExistingStatScore = (int)StartStats["score"].Value;

		if(ExistingStatScore > TopScore)
			TopScore = ExistingStatScore;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if(GameState == GameState.Playing)
		{
			ElapsedTime += Time.Delta;
			int second = MathX.FloorToInt( ElapsedTime );

			if(second != _currSecond)
			{
				_currSecond = second;
				TimeSinceNewSecond = 0f;

				int secondsRemaining = (int)GAME_TIME - second;

				if( secondsRemaining <= 10)
				{
					var beepSfx = Sound.Play( "beep", new Vector3( 0f, 0f, 100f ) );
					beepSfx.Pitch = Utils.Map( secondsRemaining, 10, 1, 0.8f, 1f, EasingType.QuadIn );
					beepSfx.Volume = Utils.Map( secondsRemaining, 10, 1, 0.8f, 1.4f, EasingType.QuadIn );
				}
			}

			if ( ElapsedTime > GAME_TIME )
			{
				var sfx = Sound.Play( "buzzer", new Vector3( 0f, 0f, 100f ) );
				sfx.Pitch = 1.05f;
				sfx.Volume = 1.5f;

				GameFinishedMessage = "Time's Up!";
				FinishGame();
			}
		}
		else if(GameState == GameState.Finished)
		{
			if( TimeSinceTally > 0.3f)
			{
				if( FinishedNumHandsTallied < FinishedSets.Count() )
				{
					LastScoreTally = Utils.GetPointsForHand( Utils.EvaluateHand( FinishedSets[FinishedNumHandsTallied] ) );
					CurrTalliedScore += LastScoreTally;

					if(LastScoreTally == 0)
					{
						var sfxNegative = Sound.Play( "negative_2", new Vector3(0f, 250f, Utils.Map( FinishedNumHandsTallied, 0, 10, 530f, 150f ) ));
						sfxNegative.Volume = 1.2f;
						sfxNegative.Pitch = 1f;
					}
					else
					{
						var sfxPositive = Sound.Play( "positive", new Vector3( 0f, 250f, Utils.Map( FinishedNumHandsTallied, 0, 10, 530f, 150f ) ) );
						sfxPositive.Volume = 1.0f;
						sfxPositive.Pitch = Utils.Map(LastScoreTally, 10, 500, 0.8f, 1.4f);
					}
				}

				FinishedNumHandsTallied++;
				TimeSinceTally = 0f;

				if( FinishedNumHandsTallied >= FinishedSets.Count())
				{
					FinishTallying();
				}
			}
		}

		if ( Input.Pressed( "reload" ) )
		{
			var sfx = Sound.Play( "switch", new Vector3(0f, -150f, 0f ));
			sfx.Pitch = Game.Random.Float( 0.85f, 0.9f );

			Restart();
		}

		if ( Input.Pressed( "use" ) )
		{
			//for (int i = 0; i < 5; i++)
			//	CollectCard( new CardData((CardSuit)Game.Random.Int(0, 3), Game.Random.Int(1, 13)) );
		}
	}

	public void Restart()
	{
		if(GameState != GameState.Playing)
		{
			if(TotalScore > TopScore)
			{
				TopScore = TotalScore;
				SaveScore();
			}

			if(TotalScore > ExistingStatScore)
				Sandbox.Services.Stats.SetValue( "score", TotalScore );
		}

		GameState = GameState.Playing;
		ElapsedTime = 0f;
		_currSecond = 0;
		GameFinishedMessage = "";
		TotalScore = 0;
		TimeSinceCollect = 0f;
		TimeSinceFinishSet = 0f;
		TimeSinceFinishGame = 0f;
		TimeSinceNewSecond = 0f;

		CollectedCards.Clear();
		FinishedSets.Clear();
		FinishedSetTimes.Clear();

		_cards.Clear();
		for ( int suit = 0; suit <= 3; suit++ ) {
			for ( int number = 1; number <= 13; number++ ) {
				_cards.Add( new CardData( (CardSuit)suit, number ) );
			}
		}

		_cards.Shuffle();

		if ( _ballContainer != null )
			_ballContainer.Destroy();

		_ballContainer = Scene.CreateObject( true );
		_ballContainer.Name = "BallContainer";

		int lockedIndex = Game.Random.Int( 0, _cards.Count - 1 );
		_numLocked = 1;

		int unknownIndex = lockedIndex;
		while ( unknownIndex == lockedIndex )
			unknownIndex = Game.Random.Int( 0, _cards.Count - 1 );
		_numUnknown = 1;

		int NUM_X = 9;
		int NUM_Y = 8;

		int index = 0;

		for ( int y = 0; y <= NUM_Y; y++ )
		{
			for ( int x = 0; x <= NUM_X; x++ )
			{
				if ( index >= _cards.Count )
					break;

				var ball = SpawnBall(
					_cards[index],
					new Vector3(
						0f,
						Utils.Map( x, 0, NUM_X, -275f, 275f ),
						Utils.Map( y, 0, NUM_Y, 650f, 1200f )
					),
					isLocked: index == lockedIndex,
					isUnknown: index == unknownIndex
				);

				index++;
			}
		}
	}

	void SaveScore()
	{
		var topScoreData = new TopScoreData() { Score = TopScore };
		FileSystem.Data.WriteJson<TopScoreData>( "ballpoker_top_score.json", topScoreData );
	}

	TestBall SpawnBall(CardData data, Vector3 pos, bool isLocked = false, bool isUnknown = false )
	{
		var ballObj = SceneUtility.Instantiate( Ball, pos, Rotation.FromYaw(90f) );
		ballObj.SetParent(_ballContainer);
		TestBall ball = ballObj.Components.Get<TestBall>();

		if(ball != null)
		{
			ball.Manager = this;
			ball.Init( data, isLocked, isUnknown );
		}

		return ball;
	}

	public void BallDestroyedInGutter(TestBall ball)
	{
		if ( ball.IsLocked )
			_numLocked--;

		if ( ball.IsUnknown )
			_numUnknown--;

		if(GameState == GameState.Playing)
		{
			SpawnReplacementBall( ball.CardData );
			SpawnGutterParticles( right: ball.Transform.Position.y > 0f );
		}

		CollectCard( ball.CardData );
		//Log.Info( $"{ball.CardData.GetDisplayString()}" );
	}

	public void CollectCard(CardData data)
	{
		if ( GameState != GameState.Playing )
			return;

		CollectedCards.Add( data );
		TimeSinceCollect = 0f;

		if (CollectedCards.Count == 5)
		{
			var set = new List<CardData>();
			set.AddRange( CollectedCards );

			FinishedSets.Add( set );
			FinishedSetTimes.Add( Time.Now );
			CollectedCards.Clear();

			TimeSinceFinishSet = 0f;

			CardHand hand = Utils.EvaluateHand( set );
			switch(hand)
			{
				case CardHand.None: 
					var sfx0 = Sound.Play( "set0" );
					sfx0.Volume = 1.0f;
					sfx0.Pitch = Game.Random.Float( 0.86f, 0.88f );
					break;
				case CardHand.OnePair: default:
					var sfx1 = Sound.Play( "set2" );
					sfx1.Volume = 1.2f;
					sfx1.Pitch = Game.Random.Float( 0.95f, 1.0f );
					break;
				case CardHand.RoyalFlush:
					var sfx9 = Sound.Play( "set9" );
					sfx9.Volume = 1.6f;
					break;
			}

			if ( FinishedSets.Count == 10)
			{
				var sfx = Sound.Play( "start0", new Vector3( 100f, 120f, 400f ) );
				sfx.Pitch = 1.15f;
				sfx.Volume = 1.5f;

				GameFinishedMessage = TimeSpan.FromSeconds( MathF.Ceiling( GAME_TIME - ElapsedTime ) ).ToString( @"m\:ss" );
				FinishGame();
			}
		}
	}

	void FinishGame() 
	{
		if ( GameState != GameState.Playing )
			return;

		GameState = GameState.Finished;
		FinishedNumHandsTallied = 0;
		TimeSinceTally = 0f;
		CurrTalliedScore = 0;
		TimeSinceFinishGame = 0f;

		TotalScore = 0;
		foreach(var set in FinishedSets)
			TotalScore += Utils.GetPointsForHand( Utils.EvaluateHand( set ) );
	}

	void FinishTallying()
	{
		if ( GameState != GameState.Finished )
			return;

		GameState = GameState.WaitingForRestart;

		var sfx = Sound.Play( "chord" );
		sfx.Pitch = Utils.Map( TotalScore, 0, 500, 0.7f, 1.4f );

		if ( TotalScore > TopScore )
		{
			TopScore = TotalScore;
			SaveScore();
		}

		if ( TotalScore > ExistingStatScore )
			Sandbox.Services.Stats.SetValue( "score", TotalScore );
	}

	void SpawnGutterParticles( bool right )
	{
		var particleObj = right
			? SceneUtility.Instantiate( GutterParticle, new Vector3( 0f, 230f, -50f ), Rotation.From( -74f, -140f, -113f ) )
			: SceneUtility.Instantiate( GutterParticle, new Vector3( 0f, -230f, -50f ), Rotation.From( -69f, 108f, -7f ) );

		particleObj.Components.Get<ParticleSpriteRenderer>().Scale = 9f;
	}

	public void BallDestroyedClick(TestBall ball)
	{
		SpawnClickRemoveParticles( ball.Transform.Position );
		SpawnReplacementBall( ball.CardData );
	}

	void SpawnClickRemoveParticles(Vector3 pos)
	{
		var particleObj = SceneUtility.Instantiate( ClickRemoveParticle, pos );
		particleObj.Components.Get<ParticleSpriteRenderer>().Scale = 9f;
	}

	void SpawnReplacementBall(CardData data)
	{
		bool isLocked = _numLocked == 0 ? Game.Random.Int(0, 10) == 0 : false;
		if ( isLocked )
			_numLocked++;

		bool isUnknown = !isLocked && _numUnknown == 0 ? Game.Random.Int( 0, 10 ) == 0 : false;
		if ( isUnknown )
			_numUnknown++;

		SpawnBall(
			data,
			new Vector3(
				0f,
				Game.Random.Float( -240f, 240f ),
				Game.Random.Float( 800f, 1200f )
			),
			isLocked,
			isUnknown
		);
	}
}
