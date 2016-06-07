using System.IO;
using System.Linq;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Text;

/**
	BaseDataを拡張してdata -> msgpack -> data を実現してるレイヤ
	string入れてるけどenumでもいいかもね。っていうかそうしようかな。
*/
public static class Commands {
	
	
	public enum CommandEnum : int {
		None,
		
		Datas,
		
		OnConnected,
		OnDisconnected,
		
		EntriedId,
		
        SpawnRequest,
		Spawn,
		
		Messaging,
		Walk,
		
		Ping,
		
		WorldData,
		
        Log,
    }
	
	[Serializable] public class PackedDatas : BaseData {
		[SerializeField] public PackedData[] datas;
		public PackedDatas (string playerId, byte[][] datas) : base (CommandEnum.Datas, playerId) {
			this.datas = datas.Select(data => new PackedData(data)).ToArray();
		}
	}
	
	[Serializable] public class PackedData {
		[SerializeField] public byte[] data;
		public PackedData (byte[] data) {
			this.data = data;
		}
	}
	
	
	[Serializable] public class OnConnected : BaseData {
		
		public OnConnected (string playerId) : base (CommandEnum.OnConnected, playerId) {
		}
	}
	
	[Serializable] public class OnDisconnected : BaseData {
		[SerializeField] public string reason;
		
		public OnDisconnected (string playerId, string reason) : base (CommandEnum.OnDisconnected, playerId) {
			this.reason = reason;
		}
	}
	
	[Serializable] public class PlayerIdAndPos {
		[SerializeField] public string playerId;
		[SerializeField] public StructVector3 pos;
		[SerializeField] public DirectionEnum dir;
		
		public PlayerIdAndPos (string playerId, StructVector3 pos, DirectionEnum dir) {
			this.playerId = playerId;
			this.pos = pos;
			this.dir = dir;
		}
	}
	
	
	[Serializable] public class StructVector2 {
		[SerializeField] public int x;
		[SerializeField] public int z;
		
		public StructVector2 (int x, int z) {
			this.x = x;
			this.z = z;
		}
	}
	
	[Serializable] public class StructVector3 {
		[SerializeField] public int x;
		[SerializeField] public int z;
		
		[SerializeField] public int height;
		
		public StructVector3 (int x, int z, int height) {
			this.x = x;
			this.z = z;
			this.height = height;
		}
	}
	
	[Serializable] public class EntriedId : BaseData {
		[SerializeField] public StructVector3 pos;
		[SerializeField] public DirectionEnum dir;
		
		public EntriedId (string playerId, StructVector3 pos, DirectionEnum dir) : base (CommandEnum.EntriedId, playerId) {
			this.pos = pos;
			this.dir = dir;
		}
	}

	
	[Serializable] public class Messaging : BaseData {
		[SerializeField] public string message;
		[SerializeField] public string targetPlayerId;
		public Messaging (string playerId, string targetPlayerId, string message) : base (CommandEnum.Messaging, playerId) {
			this.targetPlayerId = targetPlayerId;
			this.message = message;
		}
	}
	
	
	[Serializable] public class Walk : BaseData {
		[SerializeField] public DirectionEnum direction;
		[SerializeField] public StructVector3 pos;
		public Walk (string playerId, DirectionEnum direction, StructVector3 pos) : base (CommandEnum.Walk, playerId) {	
			this.direction = direction;
			this.pos = pos;
		}
	}
	
	[Serializable] public class WorldData : BaseData {
		[SerializeField] public List<PlayerIdAndPos> players;
		
		public WorldData (string playerId, List<PlayerIdAndPos> players) : base (CommandEnum.WorldData, playerId) {	
			this.players = players;
		}
	}
	
	/**
		Serverへと送り、既存のゲームへと参加する
	*/
	[Serializable] public class SpawnRequest : BaseData {
		public SpawnRequest (string playerId) : base (CommandEnum.SpawnRequest, playerId) {
			
		}
	}
	
	
	[Serializable] public class Spawn : BaseData {
		public Spawn (string playerId) : base (CommandEnum.Spawn, playerId) {
			
		}
	}
	
	
	
	/**
		送信できる辞書データの基礎クラス
	*/
	[Serializable] public class BaseData {
		[SerializeField] public CommandEnum command;
		[SerializeField] public string playerId;
		public BaseData (CommandEnum command, string playerId) {
			this.command = command;
			this.playerId = playerId;
		}
	}
	
	

	public static byte[] ToData (this Commands.BaseData data) {
		// json
		var jsonData = JsonUtility.ToJson(data);
		return Encoding.UTF8.GetBytes(jsonData.ToCharArray());
		
		// msgpack
		// return packer.Pack(data);
	}

	public static T FromData <T> (this byte[] data) where T: BaseData {
		try {
			// json
			var json = Encoding.UTF8.GetString(data);
			
			T result = JsonUtility.FromJson<T>(json);
			
			// msgpack
			// T result = packer.Unpack<T>(data);
			
			return result;	
		} catch (Exception e) {
			throw new Exception("failed to decode data. size:" + data.Length + " e:" + e + " str:" + Encoding.UTF8.GetString(data));
		}
	}
	
	
	/**
		パラメータ取り出し。定義系を書き換えて、jsonとmsgpackを行き来する感じ。
	*/
	public static CommandAndPlayerId ReadCommandAndSourceId (byte[] data) {
		using (var stream = new MemoryStream(data)) {
			// json
			Dictionary<string, object> dataDict = new Dictionary<string, object>();
			object rawObject = null;
			
			// msgpack
			// MessagePackObject rawObject;
			// MessagePackObjectDictionary dataDict;
			try {
				// json
				var dataFromJson = Commands.FromData<Commands.BaseData>(data);
				dataDict["command"] = dataFromJson.command;
				dataDict["playerId"] = dataFromJson.playerId;
				
				// msgpack
				// rawObject = Unpacking.UnpackObject(stream);
				// try {
				// 	dataDict = rawObject.AsDictionary();
				// } catch (Exception e) {
				// 	throw new Exception("failed to unpack rawObject as Dictionary. size:" + data.Length + " error:" + e);
				// } 
			} catch (Exception e) {
				throw new Exception("failed to unpack rawObject. size:" + data.Length + " error:" + e + " str:" + Encoding.UTF8.GetString(data));
			}
			
			if (!dataDict.ContainsKey("command")) throw new Exception("failed to read command from client:" + rawObject + " size:" + data.Length);
			if (!dataDict.ContainsKey("playerId")) throw new Exception("failed to read playerId from client:" + rawObject + " size:" + data.Length);
			
			try {
				// json
				var command = (Commands.CommandEnum)dataDict["command"];
				var playerId = dataDict["playerId"].ToString();
				
				// msgpack
				// var command = (Commands.CommandEnum)dataDict["command"].AsInt32();
				// var playerId = dataDict["playerId"].AsStringUtf8();
				
				return new CommandAndPlayerId(command, playerId);
			} catch (Exception e) {
				throw new Exception("failed to unpack data. size:" + data.Length + " error:" + e);
			}
		}
	}
	
	public struct CommandAndPlayerId {
		public Commands.CommandEnum command;
		public string playerId;
		
		public CommandAndPlayerId (Commands.CommandEnum command, string playerId) {
			this.command = command;
			this.playerId = playerId;
		}
	}
	
}