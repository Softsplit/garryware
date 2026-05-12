public sealed class WareThrownCrowbarWeapon : BaseWeapon
{
	private static readonly Vector3 FallbackColliderStart = new( -18f, 0f, 0f );
	private static readonly Vector3 FallbackColliderEnd = new( 18f, 0f, 0f );
	private const float ThrowForwardOffset = 16f;
	private const float FallbackColliderRadius = 4f;

	[Property] public SoundEvent ThrowSound { get; set; }
	[Property] public float ProjectileForce { get; set; } = 100000f;
	[Property] public string ProjectileModel { get; set; } = "models/weapons/sbox_melee_crowbar/w_crowbar.vmdl";

	private bool _hasThrown;

	protected override float GetPrimaryFireRate() => 0.3f;
	public override bool CanSecondaryAttack() => false;

	public override bool CanPrimaryAttack()
	{
		if ( _hasThrown ) return false;
		return base.CanPrimaryAttack();
	}

	public override void PrimaryAttack()
	{
		if ( _hasThrown ) return;

		_hasThrown = true;
		AddShootDelay( 0.3f );

		if ( ViewModel.IsValid() )
			ViewModel.RunEvent<ViewModel>( x => x.OnAttack() );
		else if ( WorldModel.IsValid() )
			WorldModel.RunEvent<WorldModel>( x => x.OnAttack() );

		if ( ThrowSound.IsValid() )
			GameObject.PlaySound( ThrowSound );

		var ray = AimRay;
		ThrowAndStrip( ray.Position + ray.Forward * ThrowForwardOffset, ray.Forward, ProjectileForce );
	}

	[Rpc.Host]
	private void ThrowAndStrip( Vector3 start, Vector3 direction, float force )
	{
		var go = WareProjectileFactory.CreatePhysicsModelProjectile( "swent_crowbar", start, direction, ProjectileModel, FallbackColliderStart, FallbackColliderEnd, FallbackColliderRadius );
		var projectile = go.Components.Create<WareThrownCrowbarProjectile>();
		projectile.Instigator = Owner;
		go.NetworkSpawn();
		projectile.Rigidbody.AngularVelocity = new Vector3( Random.Shared.Float( -600f, 600f ), Random.Shared.Float( -600f, 600f ), Random.Shared.Float( -600f, 600f ) );
		projectile.Rigidbody.ApplyImpulse( direction.Normal * force );

		Owner.GetComponent<PlayerInventory>()?.Remove( this );
	}
}