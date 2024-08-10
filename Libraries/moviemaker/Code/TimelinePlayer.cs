namespace Sandbox.MovieMaker;

[Category( "Movie Maker" )]
[Alias( "TimelineTest", "TimelinePlayer" )]
public sealed class MovieClipPlayer : Component
{
	[Property, Hide]
	public MovieClip clip { get; set; }

	[Property]
	public bool Play { get; set; } = true;

	[Property]
	public bool Loop { get; set; } = true;

	[Property, Range( 0, 2 )]
	public float PlaybackSpeed { get; set; } = 1.0f;


	public float Position;

	protected override void OnEnabled()
	{
		clip ??= new MovieClip();
	}

	protected override void OnUpdate()
	{
		if ( Play )
		{
			clip.ScrubTo( Position );

			Position += Time.Delta * PlaybackSpeed;

			if ( Position > clip.Duration )
			{
				Position = clip.Duration;

				if ( Loop )
					Position = 0;
			}
		}
	}
}
