public sealed class WareRingZone : Component
{
	private const string SpritePath = "textures/sprites/sent_ball.svg";
	private SpriteRenderer _renderer;
	private static Sprite _sprite;

	[Sync] public float Radius { get; set; }
	[Sync] public float Height { get; set; } = 2f;
	[Sync] public Color ZoneColor { get; set; } = Color.FromBytes( 185, 220, 255 );

	protected override void OnEnabled()
	{
		_renderer = Components.GetOrCreate<SpriteRenderer>();
		_renderer.Sprite = GetSprite();
		_renderer.Billboard = SpriteRenderer.BillboardMode.None;
		_renderer.Lighting = false;
		_renderer.Shadows = false;
		_renderer.IsSorted = true;
		_renderer.FogStrength = 0f;
	}

	protected override void OnUpdate()
	{
		if ( !_renderer.IsValid() ) return;

		WorldRotation = Rotation.LookAt( Vector3.Up, Vector3.Forward );
		_renderer.Size = new Vector2( Radius * 2f, Radius * 2f );
		_renderer.Color = ZoneColor;
	}

	private static Sprite GetSprite()
	{
		return _sprite ??= Sprite.FromTexture( Texture.Load( SpritePath ) );
	}
}