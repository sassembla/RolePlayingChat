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
		
		
		Message,
		
		ExecutedOnClient,
		ExecutedOnServer,
		
		OnConnected,
		OnDisconnected,
		
		EntriedId,
		PlayerLeft,
		
        Resetted,
        SpawnRequest,
        Action,
        GameData,
        FrameData,
        PlayerPos,
		
		
        Log,
    }
	
	[Serializable] public class Datas : BaseData {
		[SerializeField] public CommandEnum[] commands;
		[SerializeField] public PackedData[] datas;
		public Datas (string playerId, CommandEnum[] commands, byte[][] datas) : base (CommandEnum.Datas, playerId) {
			this.commands = commands;
			this.datas = datas.Select(data => new PackedData(data)).ToArray();
		}
	}
	
	[Serializable] public class PackedData {
		[SerializeField] public byte[] data;
		public PackedData (byte[] data) {
			this.data = data;
		}
	}
	
	
	public class OnConnected : BaseData {
		
		public OnConnected (string playerId) : base (CommandEnum.OnConnected, playerId) {
		}
	}
	
	public class OnDisconnected : BaseData {
		public string reason;
		
		public OnDisconnected (string playerId, string reason) : base (CommandEnum.OnDisconnected, playerId) {
			this.reason = reason;
		}
	}
	
	/**
		Serverへと送り、接続車としてのPlayerIdを伝える
	*/
	public class EntriedId : BaseData {
		public int side;
		public EntriedId (string playerId, int side) : base (CommandEnum.EntriedId, playerId) {
			this.side = side;
		}
	}
	
	public class PlayerLeft : BaseData {
		public int reasonCode;
		
		public PlayerLeft (string playerId, int reasonCode) : base (CommandEnum.PlayerLeft, playerId) {
			this.reasonCode = reasonCode;
		}
	}
	
	/**
		意味を持たせない単なるメッセージ
	*/
	public class Message : BaseData {
		public string message;
		public Message (string playerId, string message) : base (CommandEnum.Message, playerId) {
			this.message = message;
		} 
	}
	
	/**
		あらゆるXrossPeerのメソッド実行記録
	*/
	public class ExecutedOnClient : BaseData {
		public string executedMethodName;
		public string newAutoName;
		
		public ExecutedOnClient (string playerId, string executedMethodName, string newAutoName) : base (CommandEnum.ExecutedOnClient, playerId) {
			this.executedMethodName = executedMethodName;
			this.newAutoName = newAutoName;
		}
	}
	
	public class ExecutedOnServer : BaseData {
		public string executedMethodName;
		public string newAutoName;
		
		public ExecutedOnServer (string playerId, string executedMethodName, string newAutoName) : base (CommandEnum.ExecutedOnServer, playerId) {
			this.executedMethodName = executedMethodName;
			this.newAutoName = newAutoName;
		}
	}
	

	/**
		サーバ側からのリセット通知
	*/
	public class Resetted : BaseData {
		public Resetted () : base (CommandEnum.Resetted, string.Empty) {}
	}


	/**
		Serverへと送り、既存のゲームへと参加する
	*/
	public class SpawnRequest : BaseData {
		public int index;
		public SpawnRequest (string playerId, int index) : base (CommandEnum.SpawnRequest, playerId) {
			this.index = index;
		}
	}
	
	/**
		アクションのトリガーをServerに送付
		8方向くらい用意するなら違った話になる。とりあえず、移動と基本攻撃を入れておく。
	*/
	public class Action : BaseData {
		public bool front;
		public bool back;
		public bool right;
		public bool left;
		public bool up;
		public bool down;
		public bool attack;
		public bool fire;
		public float fx;
		public float fy;
		public float fz;

		public Action (string playerId, bool front, bool back, bool right, bool left, bool up, bool down, bool attack, 
			bool fire, float fx, float fy, float fz) : base (CommandEnum.Action, playerId) {
			this.front = front;
			this.back = back;

			this.right = right;
			this.left = left;
			
			this.up = up;
			this.down = down;

			this.attack = attack;

			this.fire = fire;
			this.fx = fx;
			this.fy = fy;
			this.fz = fz;

		}
	}

	/**
		ゲームの状態を取得、Spawn可能なところまでクライアント状態を持っていく。
	*/
	public class GameData : BaseData {
		public int frame;
		
		public int positionIndex;
		
		public GameData (string playerId, int frame, int positionIndex) : base (CommandEnum.GameData, playerId) {
			// 存在しているplayerIdや、どんなジョブか、などをクライアントに送付する。
			/*
				playerId
					pos
					job
					hp
					他
			*/
			this.frame = frame;
			this.positionIndex = positionIndex;
		}
	}

	/**
		Serverからのframeデータを受け取り、ゲームに反映させる。
		コア
	*/
	public class FrameData : BaseData {
		public int frame;
		// public Dictionary<string, PlayerPos> playersPos;
		// public List<Record.RecordBase> playersFrameActions;

		public FrameData (string playerId, int frame, Dictionary<string, PlayerPos> playersPosDict) : base (CommandEnum.FrameData, playerId) {
			this.frame = frame;
			// this.playersPos = playersPosDict;
			// this.playersFrameActions = new List<Record.RecordBase>();
		}
		
		// public FrameData (int frame, Dictionary<string, PlayerPos> playersPosDict, List<Record.RecordBase> playersFrameActions) : base ("FrameData") {
		// 	this.frame = frame;
		// 	// this.playersPos = playersPosDict;
		// 	// this.playersFrameActions = playersFrameActions;
		// }
	}

	/**
		プレイヤーの位置情報	を受け取る
	*/
	public class PlayerPos : BaseData {
		public int x;
		public int y;
		public int z;
		public int dir;

		public PlayerPos (string playerId, int x, int y, int z, int dir) : base (CommandEnum.PlayerPos, playerId) {
			this.x = x;
			this.y = y;
			this.z = z;
			this.dir = dir;
		}
	}

	/**
		ゲームサーバへとlogを送付する
	*/
	public class Log : BaseData {
		public string log;
		public Log (string playerId, string log) : base (CommandEnum.Log, playerId) {
			this.log = log;
		}
	}



	/**
		送信できる辞書データの基礎クラス
		お、この型の拡張メソッド作れば良いことありそう。各拡張で勝手に自分の型で取り出せるとちょっとらく。
	*/
	public class BaseData {
		public CommandEnum command;
		public string playerId;
		public BaseData (CommandEnum command, string playerId) {
			this.command = command;
			this.playerId = playerId;
		}
	}

	/**
		変換。
	*/
	// private static ObjectPacker packer = new ObjectPacker();
	

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
			throw new Exception("failed to decode data. size:" + data.Length + " e:" + e);
		}
	}
	
	
	/**
		定義系を書き換えて、jsonとmsgpackを行き来する感じ。
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
				throw new Exception("failed to unpack rawObject. size:" + data.Length + " error:" + e);
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