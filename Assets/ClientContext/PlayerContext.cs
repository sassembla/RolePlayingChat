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

public struct AutoInfo {
	public string autoName;
	public List<string> parameters;

	public AutoInfo (string autoName, List<string> parameters) {
		this.autoName = autoName;
		this.parameters = parameters;
	}
}

public class PlayerContext {
	public Auto<PlayerContext, List<PlayerContext>> auto;
	public List<AutoInfo> stackedDummyAutos;
	public readonly string playerId;
	
	public DirectionEnum forward;
	
	public float x=0, z=0, height=0;
	
	public string motionName = string.Empty;
	
	public string talkablePlayerId = string.Empty;
	public string lastTalkedPlayerId = string.Empty;
	
	public string talkingPlayerId = string.Empty;
	
	public string messageSend = string.Empty;

	public bool isDummy;
	
	public string dummyMessage = string.Empty;
	public string dummyTargetId = string.Empty;


	public List<Commands.BaseData> stackedCommands;
	
	public PlayerContext (string playerId, Commands.StructVector3 pos, DirectionEnum dir) {
		this.playerId = playerId;
		this.forward = DirectionEnum.North;
		this.x = (int)(pos.x * RolePlayingChatDefinitions.FloorUnit);
		this.z = (int)(pos.z * RolePlayingChatDefinitions.FloorUnit);
		this.height = pos.height;
		this.forward = dir;

		this.stackedDummyAutos = new List<AutoInfo>();
	}
	
	public Commands.StructVector3 Position () {
		return new Commands.StructVector3((int)this.x, (int)this.z, (int)this.height);
	}
}