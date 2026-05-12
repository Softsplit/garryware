public class GlockWeapon : IronSightsWeapon
{
	[Property] public float PrimaryFireRate { get; set; } = 0.15f;

	protected override float GetPrimaryFireRate() => PrimaryFireRate;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	public override void PrimaryAttack()
	{
		ShootBullet( PrimaryFireRate, GetBullet() );
	}
}
