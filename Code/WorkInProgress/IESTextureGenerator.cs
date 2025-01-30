using System;
using System.IO;
using System.Threading;
using Sandbox;

public class IES : Sandbox.Resources.TextureGenerator
{
	internal static bool IsAppropriate( string url )
	{
		return url.EndsWith( ".ies" );
	}

	public struct IESInfo
	{
		public int NumLamps;
		public float LumensPerLamp;
		public float CandelaMultiplier;

		public int NumHorizontal;
		public int NumVertical;

		public int PhotometricType;
		public int UnitsType;
		
		public float Width;
		public float Length;
		public float Height;

		public float BallastFactor;
		public float Unused;
		public float InputWatts;
		
		public List<float> VerticalAngles;
		public List<float> HorizontalAngles;
		public List<float> Candelas;
	}

	internal static IESInfo ParseIESInfo( Stream stream )
	{
		var reader = new StreamReader( stream );
		string version = reader.ReadLine();

		// Read until tilt information, we don't care the metadata that's behind
		while ( !reader.EndOfStream )
		{
			var line = reader.ReadLine();
			if ( line.StartsWith( "TILT=" ) )
			{
				var tilt = line.Substring( 5 );
				if ( tilt != "NONE" )
				{
					throw new Exception( "Tilted IES files are not supported" );
				}
				break;
			}
		}

		if( reader.EndOfStream )
		{
			throw new Exception( "IES file is missing TILT information" );
		}

		// We get the light info on this line
		var lightInfo = reader.ReadToEnd().Split( new string[] { " ", "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries );
		
		IESInfo info = new IESInfo
		{
			NumLamps = int.Parse( lightInfo[0] ),
			LumensPerLamp = float.Parse( lightInfo[1] ),
			CandelaMultiplier = float.Parse( lightInfo[2] ),
			NumVertical = int.Parse( lightInfo[3] ),
			NumHorizontal = int.Parse( lightInfo[4] ),
			PhotometricType = int.Parse( lightInfo[5] ),
			UnitsType = int.Parse( lightInfo[6] ),
			Width = float.Parse( lightInfo[7] ),
			Length = float.Parse( lightInfo[8] ),
			Height = float.Parse( lightInfo[9] ),
			BallastFactor = float.Parse( lightInfo[10] ),
			Unused = float.Parse( lightInfo[11] ),
			InputWatts = float.Parse( lightInfo[12] ),

			VerticalAngles = new List<float>(),
			HorizontalAngles = new List<float>(),
			Candelas = new List<float>()
		};

		int i = 13;

		for ( int y = 0; y < info.NumVertical; y++, i++ )
		{
			info.VerticalAngles.Add( float.Parse( lightInfo[i] ) );
		}

		for ( int x = 0; x < info.NumHorizontal; x++, i++ )
		{
			info.HorizontalAngles.Add( float.Parse( lightInfo[i] ) );
		}

		for ( int x = 0; x < info.NumHorizontal; x++ )
		{
			for ( int y = 0; y < info.NumVertical; y++, i++ )
			{
				info.Candelas.Add( float.Parse( lightInfo[i] ) );
			}
		}
		return info;
	}

	public static float Sample1D( IESInfo info, float angle )
	{
		float angleDegrees = angle;
		int index = 0;

		//Outside
		if ( angle > info.VerticalAngles.Last() )
			return 0;
		
		// Find the right index
		for( int i=0; i < info.VerticalAngles.Count; i++ )
		{
			if ( info.VerticalAngles[i] > angleDegrees )
			{
				index = i;
				break;
			}
		}

		index = index.Clamp( 1, info.VerticalAngles.Count );

		// Interpolate between the two closest values
		float a = info.VerticalAngles[index - 1];
		float b = info.VerticalAngles[index];
		float percentage = (angleDegrees - a) / (b - a);

		float c = info.Candelas[index - 1];
		float d = info.Candelas[index];

		return c.LerpTo( d, percentage );
	}
	
	public static Texture Generate2DTexture( IESInfo info )
	{
		const int width = 512;
		const int height = 512;
		
		float invW = 1.0f / width;
		float invH = 1.0f / height;
		float invMaxValue = 1.0f / ( info.Candelas.Max() );

		float maxAngle = info.VerticalAngles.Max();

		byte[] imageData = new byte[width * height * 4];

		for ( int y = 0; y < height; ++y )
		{
			for ( int x = 0; x < width; ++x )
			{
				float distance = MathF.Sqrt( (x * invW - 0.5f) * (x * invW - 0.5f) + (y * invH - 0.5f) * (y * invH - 0.5f) )  * maxAngle;

				// Sample the IESInfo candela at the angle position
				float candela = Math.Clamp( Sample1D( info, distance ) * invMaxValue, 0.0f, 1.0f );

				bool isEven = (x % 2 == 0) && (y % 2 == 0);

				// Dither it to make it look a bit better on a low precision color space (like RGB8)
				int sample =  (int)(candela * 255.0f); // ( int)Math.Floor( candela * 255.0f ) : (int)Math.Round( candela * 255.0f );

				// sample to our imagedata
				int index = (y * width + x) * 4;
				imageData[index + 0] = (byte)(sample);
				imageData[index + 1] = (byte)(sample);
				imageData[index + 2] = (byte)(sample);
				imageData[index + 3] = 255;
			}
		}

		return Texture.Create( width, height ).WithName( $"_IESLight{ info.GetHashCode() }" ).WithData( imageData ).Finish();
	}
	
	public static Texture Load( Stream stream )
	{
		IESInfo info = ParseIESInfo( stream );
			
		var placeholder = Generate2DTexture( info );
		return placeholder;
	}

	[TextArea] public string Source { get; set; }
	protected override ValueTask<Texture> CreateTexture( Options options, CancellationToken ct )
	{
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Source));
		return ValueTask.FromResult( Load( stream ) );
	}

}
