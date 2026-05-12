using System.Globalization;

public readonly record struct WareHudMessage( string Text, int BackgroundR, int BackgroundG, int BackgroundB, int ForegroundR, int ForegroundG, int ForegroundB, float Started, float Duration, int Version )
{
	private const float AppearDuration = 0.16f;

	public bool IsVisible => !string.IsNullOrWhiteSpace( Text ) && Age < Duration;
	public float Age => Time.Now - Started;
	public float Visibility => Age > AppearDuration ? 1f - MathF.Pow( Math.Clamp( Age / Duration, 0f, 1f ), 3f ) : Math.Clamp( Age / AppearDuration, 0f, 1f );
	public float TextVisibility => Age > AppearDuration ? Visibility : 1f;

	public string PanelStyle( float screenY )
	{
		return string.Create( CultureInfo.InvariantCulture, $"top:{screenY}%;" );
	}

	public string FillStyle()
	{
		return string.Create( CultureInfo.InvariantCulture, $"background-color:rgba({BackgroundR},{BackgroundG},{BackgroundB},{Visibility});" );
	}

	public string BorderStyle()
	{
		return string.Create( CultureInfo.InvariantCulture, $"background-color:rgba(255,255,255,{0.86f * Visibility});" );
	}

	public string TextStyle()
	{
		return string.Create( CultureInfo.InvariantCulture, $"color:rgba({ForegroundR},{ForegroundG},{ForegroundB},{TextVisibility});" );
	}
}

public static class WareHudState
{
	public static WareHudMessage Instruction { get; private set; }
	public static WareHudMessage Status { get; private set; }
	public static int Version { get; private set; }

	public static void ShowInstruction( string text )
	{
		Instruction = CreateMessage( text, 128, 170, 128, 255, 255, 255, 5f );
	}

	public static void ShowStatus( string text, bool positive, float duration = 3f )
	{
		Status = positive
			? CreateMessage( text, 0, 164, 237, 255, 255, 255, duration )
			: CreateMessage( text, 255, 87, 87, 255, 255, 255, duration );
	}

	public static void ClearStatus()
	{
		Status = default;
		Version++;
	}

	public static void Clear()
	{
		Instruction = default;
		Status = default;
		Version++;
	}

	private static WareHudMessage CreateMessage( string text, int backgroundR, int backgroundG, int backgroundB, int foregroundR, int foregroundG, int foregroundB, float duration )
	{
		Version++;
		return new WareHudMessage( text, backgroundR, backgroundG, backgroundB, foregroundR, foregroundG, foregroundB, Time.Now, duration, Version );
	}
}