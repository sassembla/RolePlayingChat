using System.Collections.Generic;
using Automatine;

public enum DirectionEnum {
	None,
	North,
	East,
	South,
	West
}

public class PlayerContext {
	public Auto<PlayerContext, List<PlayerContext>> auto;
	public readonly string playerId;
	
	public DirectionEnum forward;
	
	public float x=0, z=0, height=30;
	
	public string motionName = string.Empty;
	
	public PlayerContext (string playerId) {
		this.playerId = playerId;
		this.forward = DirectionEnum.North;
	}
	
}