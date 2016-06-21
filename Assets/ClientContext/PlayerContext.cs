using System.Collections.Generic;
using Automatine;
using UnityEngine;

public enum KeyEnum {
	None,
	Send
}


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
	
	public string talkablePlayerId = string.Empty;
	public string lastTalkedPlayerId = string.Empty;
	
	public string talkingPlayerId = string.Empty;
	
	public string messageSend = string.Empty;

	public bool isDummy;
	
	public List<Commands.BaseData> stackedCommands;
	
	public PlayerContext (string playerId, Commands.StructVector3 pos, DirectionEnum dir) {
		this.playerId = playerId;
		this.forward = DirectionEnum.North;
		this.x = (int)(pos.x * RolePlayingChatDefinitions.FloorUnit);
		this.z = (int)(pos.z * RolePlayingChatDefinitions.FloorUnit);
		this.height = pos.height;
		this.forward = dir;
	}
	
	public Commands.StructVector3 Position () {
		return new Commands.StructVector3((int)this.x, (int)this.z, (int)this.height);
	}
}