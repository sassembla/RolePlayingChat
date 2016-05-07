using System.Collections.Generic;
using Automatine;
using UnityEngine;

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
	
	public float x=0, z=0, height=0;
	
	public string motionName = string.Empty;
	
	public PlayerContext (string playerId, Commands.StructVector3 pos) {
		this.playerId = playerId;
		this.forward = DirectionEnum.North;
		this.x = pos.x * RolePlayingChatDefinitions.FloorUnit;
		this.z = pos.z * RolePlayingChatDefinitions.FloorUnit;
		this.height = pos.height;
		Debug.LogError("x:" + x);
	}
	
}