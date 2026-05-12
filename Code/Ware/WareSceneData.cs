[Category( "GarryWare" ), Icon( "meeting_room" )]
public sealed class WareRoomComponent : Component
{
	[Property] public int MinPlayers { get; set; }
	[Property] public int MaxPlayers { get; set; } = 32;
}

[Category( "GarryWare" ), Icon( "place" )]
public sealed class WareLocationComponent : Component
{
	[Property] public WareRoomComponent Room { get; set; }
}
