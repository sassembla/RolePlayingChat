using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using XrossPeerUtility;

/**
	このレイヤーで、ゲームの参加者、総合的なstateの判断を行う。
	XrossPeerを内包する。
	
	このレイヤーでconnectionIdとplayerIdを交換、
	XrossPeerに対しては、playerIdのみを露出させる。
*/
public class GameContextLayer {
	private World world;
	
	/**
		playerId, connectionidとdataをパッケージにする
	*/
	private struct DataPack {
		public readonly string playerId;
		public readonly byte[] data;
		public DataPack (string playerId, byte[] data) {
			this.playerId = playerId;
			this.data = data;
		}
	}
	
	private class PlayerSlot {
		public readonly string playerId;
		public string connectionId;
		
		public PlayerSlot (string playerId) {
			this.playerId = playerId;
		}
	}
	
	private PlayerSlot[] connections;
	
	
	
	
	private Queue<DataPack> dataQueue = new Queue<DataPack>();
	
	private readonly string gameLayerId;
	
	private BattleState state = BattleState.STATE_READY;
	
	
	// private XrossPeerContext xrossPeerContext;
	/*
		publish data to specific connection.
	*/
	Action<string, byte[]> Publish;
	
	/*
		stack data for publish for each connection.
	*/
	private void StackPublish (Commands.BaseData data, string[] connectionIds) {
		foreach (var connectionId in connectionIds) {
			if (!stackedData.ContainsKey(connectionId)) stackedData[connectionId] = new List<Commands.BaseData>(); 
			stackedData[connectionId].Add(data);
		}
	}
	
	
	private Dictionary<string, List<Commands.BaseData>> stackedData = new Dictionary<string, List<Commands.BaseData>>();
	
	private void PublishStackedData () {
		if (!stackedData.Any()) return;
		
		foreach (var connectionId in stackedData.Keys) {
			var playerId = PlayerIdFromConnectionId(connectionId);
			var datas = stackedData[connectionId];
			
			if (datas.Count == 1) {
				Publish(connectionId, datas[0].ToData());
				continue;
			}
			
			// publish as multiple combined data.
			var byteDatas = stackedData[connectionId].Select(command => command.ToData()).ToArray();
			var combinedData = new Commands.Datas(playerId, byteDatas).ToData();
			
			Publish(connectionId, combinedData);
		}
		
		// clear list.
		stackedData.Clear();
	}
	
	
	
	public GameContextLayer (List<string> reservedPlayerIds, Action<string, byte[]> publish) {
		gameLayerId = Guid.NewGuid().ToString();
		
		world = new World();
		
		/*
			ready player's connection slot.
		*/
		connections = new PlayerSlot[reservedPlayerIds.Count];
		foreach (var reservedPlayerId in reservedPlayerIds.Select((val, index) => new {val, index})) {
			connections[reservedPlayerId.index] = new PlayerSlot(reservedPlayerId.val);
		}
		
		// xrossPeerContext = new XrossPeerContext();
		
		
		/*
			この送信メソッドはフレーム末尾でまとめて行うのに使用する。
		*/
		Publish = (string connectionId, byte[] data) => {
			publish(connectionId, data);
		};
		
		// start dequeOnFrame to loop.
		new Updater("gameLayer_" + gameLayerId, UpdateGameLayer);
	}
	
	public bool SetConnectionIdOfPlayerId (string playerId, string connectionId) {
		var playerSlotIndex = Array.FindIndex(connections, connection => connection.playerId == playerId);
		if (-1 < playerSlotIndex) {
			connections[playerSlotIndex].connectionId = connectionId;
			return true;
		}
		
		XrossPeer.Log("failed to set connectionId to playerId:" + playerId);
		return false;
	}
	
	public bool DiscardConnectionIdOfPlayerId (string playerId) {
		var playerSlotIndex = Array.FindIndex(connections, connection => connection.playerId == playerId);
		if (-1 < playerSlotIndex) {
			connections[playerSlotIndex].connectionId = string.Empty;
			return true;
		}
		
		XrossPeer.Log("failed to discard connectionId to playerId:" + playerId);
		return false;
	}
	
	private int gameFrame;
	
	/*
		enque data on receive.
	*/
	public void EnqueOnReceive (string connectionId, byte[] data) {
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "接続者が他人のplayerIdでデータを送ってきた場合、データを展開した瞬間にバレさせることができる。一度発見したら強制切断しよう。");
		
		var playerId = PlayerIdFromConnectionId(connectionId);
		if (string.IsNullOrEmpty(playerId)) {
			XrossPeer.Log("不明なconnectionIdからのデータ:" + connectionId + " 内容は、:" + Encoding.UTF8.GetString(data));
			return;
		}
		
		lock (dataQueue) dataQueue.Enqueue(new DataPack(playerId, data));
	}
	
	public void EnqueOnDisconnect (string playerId, byte[] data) {
		lock (dataQueue) dataQueue.Enqueue(new DataPack(playerId, data));
	}
	
	/**
		main loop of GameContext.
	*/
	public bool UpdateGameLayer () {
		switch (state) {
			case BattleState.STATE_READY: {
				XrossPeer.TimeAssert(Develop.TIME_ASSERT, "絶賛準備中のステート、2フレームくらい回してみよう。clientのリトライを試す、、のはあとで。");
				if (true) {
					XrossPeer.TimeAssert(Develop.TIME_ASSERT, "Server起動中、準備中など。いろんなConnectionとかロードが完了したらプレイヤーがいない世界へ遷移");
					state = BattleState.STATE_NOPLAYERS;
				}
				break;
			}
			
			
			/*
				ゲームが稼働しているステート
			*/
			case BattleState.STATE_NOPLAYERS: 
			case BattleState.STATE_PLAYERS_EXISTS: {
				// if (xrossPeerContext.PlayerExists()) {
				// 	XrossPeer.TimeAssert(Develop.TIME_ASSERT, "ゲームにプレイヤーがいることでの時間制限とかを付けるならこのへん。");
				// 	state = BattleState.STATE_PLAYERS_EXISTS;
				// }
				
				UpdateXrossPeer(gameFrame);
				
				// XrossPeer.Log("仮でメッセージを送っていた。");
				// StackPublish(new Commands.Message("aaa", "message from server."), AllConnectedIds());
				
				gameFrame++;
				break;
			}
			case BattleState.STATE_ENDING: {
				XrossPeer.TimeAssert(Develop.TIME_ASSERT, "とりあえずリセットをかける。クライアント側にはサーバリセット後の通信が届くので、そこから勝手に復帰させるといい感じになる。");
				
				// 時間稼ぎとか。UniRxで10秒とかを計ると良いんだと思う。 STATE_ENDED
				state = BattleState.STATE_ENDED;
				break;
			}
			case BattleState.STATE_ENDED: {
				// do nothing yet. 
				return false;//SAYONARA!!!
			}
			default: {
				XrossPeer.Log("message received at missing state:" + state);
				break;
			}
		}
		
		/*
			send stacked data to players. 
		*/
		PublishStackedData();
		
		return true;
	}
	
	
	private void UpdateXrossPeer (int frame) {
		lock (dataQueue) {
			while (dataQueue.Any()) {
				var dataPack = dataQueue.Dequeue();
				InputToXrossPeer(gameFrame, dataPack.playerId, dataPack.data);
			}
		};
		
		// xrossPeerContext.Update(frame);
	}
	
	
	/**
		ServerContext Core.
		このレイヤがXrossPeerの外側一枚。
	*/
	private void InputToXrossPeer (int frame, string playerId, byte[] data) {
		/*
			validate data.
		*/
		var commandAndPlayerId = Commands.ReadCommandAndSourceId(data);
		var command = commandAndPlayerId.command;
		var commandPlayerId = commandAndPlayerId.playerId;
		
		if (commandPlayerId != playerId) {
			XrossPeer.Log("実行者と送付者のidがマッチしない、BANする");
			return;
		} 
		
		/*
			extract.
			この部分で、
			・XrossPeer内の対象のAutoが状況変化を受けいれること
			・対象変化を受け入れた場合、そのコマンドを全員に配ること
			・受け入れない場合ここで黙殺すること
			を決める。
		*/
		
		/*
			サーバでしか受け取らず、サーバでないと他のユーザーに反射しないシリーズ。
			ユーザーの有無っていう究極的な可否を判定する根っこ。
			
			だいたいConnectionServerが吐いてる
		*/
		switch (command) {
			
			case Commands.CommandEnum.OnConnected: {
				var onConnected = Commands.FromData<Commands.OnConnected>(data);
				var onConnectedPlayerId = onConnected.playerId;
				
				XrossPeer.Log("connected playerId:" + onConnectedPlayerId);
				
				/*
					このタイミングで、サーバへのプレイヤーのログインが完了してる。
				*/
				{
					// 適当な位置をでっち上げる
					var x = Convert.ToInt32(onConnectedPlayerId)%20;
					
					var newPlayer = new PlayerInServer(onConnectedPlayerId, x, 0, 30, DirectionEnum.East);
					world.AddPlayer(newPlayer);
					StackPublish(new Commands.EntriedId(onConnectedPlayerId, newPlayer.pos, newPlayer.dir), AllConnectedIds());
				}
				
				/*
					ダミーを10人くらい降らせよう。
				*/
				if (AllConnectedIds().Length == 1) {
					XrossPeer.Log("最初の接続者 onConnectedPlayerId:" + onConnectedPlayerId + " が来たので、ダミーを降らせる。ランダムシードがすげえーー怪しい時があるな。");
					var xRand = new byte[10];
					var zRand = new byte[10];
					
					var r1 = new System.Random();
					r1.NextBytes(xRand);
					
					var r2 = new System.Random();
					r2.NextBytes(zRand);
					
					for (var i = 0; i < 10; i++) {
						var dummyPlayerId = Guid.NewGuid().ToString();
						var dir = DirectionEnum.South;
						var dummyPlayer = new PlayerInServer(dummyPlayerId, xRand[i]%40, zRand[i]%40, 30, dir, true);
						world.AddPlayer(dummyPlayer);
						// StackPublish(new Commands.EntriedId(dummyPlayerId, dummyPlayer.pos, dummyPlayer.dir), AllConnectedIds());
					}
				}
				
				var playersInfos = world.PlayersInfos();
				StackPublish(new Commands.WorldData(onConnectedPlayerId, playersInfos), new string[]{ConnectionIdFromPlayerId(onConnectedPlayerId)});				
				
				return;
			}
			case Commands.CommandEnum.OnDisconnected: {
				var onDisconnected = Commands.FromData<Commands.OnDisconnected>(data);
				var disconnectedPlayerId = onDisconnected.playerId;
				var reason = onDisconnected.reason;
				
				var reasonCode = 0;
				
				XrossPeer.Log("disconnected この時点で通信対象リストからは外されている。 disconnectedPlayerId:" + disconnectedPlayerId + " reason:" + reason + " まだなんにもしてない。");
				// StackPublish(new Commands.PlayerLeft(disconnectedPlayerId, reasonCode), AllConnectedIds());
				return;
			}
		}
		
		
		switch (command) {
			
			case Commands.CommandEnum.SpawnRequest: {
					
				var spawnData = Commands.FromData<Commands.SpawnRequest>(data);
				
				var spawnPlayerId = spawnData.playerId;
				XrossPeer.Log("spawnがきた spawnPlayerId:" + spawnPlayerId);
				
				/*
					すでにSpawnしてないかとかみて、OKだったらSpawnを返す
				*/
				StackPublish(new Commands.Spawn(spawnPlayerId), AllConnectedIds());
				return;
			}
			
			// 移動データとかを扱う。
			case Commands.CommandEnum.Walk: {
				var walkData = Commands.FromData<Commands.Walk>(data);
				var walkingPlayerId = walkData.playerId;
				var walkingDir = walkData.direction;
				var walkBasePos = walkData.pos;
				
				StackPublish(new Commands.Walk(walkingPlayerId, walkingDir, walkBasePos), AllConnectedIds());
				return;
			}
			case Commands.CommandEnum.SendMessage: {
				var messageData = Commands.FromData<Commands.SendMessage>(data);
				var senderPlayerId = messageData.playerId;
				var message = messageData.message;
				var targetPlayerId = messageData.targetPlayerId;
				
				XrossPeer.Log("message:" + message + " from playerId:" + senderPlayerId + " to targetPlayerId:" + targetPlayerId);
				
				XrossPeer.Log("対象のユーザーに向けて送付する(CPUかどうかの判定がついに必要になってきたな、、適当なbot化をやってみるか。)");
				
				return;
			}
			default: {
				XrossPeer.Log("gameContextLayer unhandled command:" + command);
				break;
			}
		}
	}
	
	private string[] AllConnectedIds () {
		return connections.Where(con => !string.IsNullOrEmpty(con.connectionId)).Select(con => con.connectionId).ToArray();
	}
	
	private string[] AllConnectedIdsExcept (params string[] exceptConnectionIds) {
		var connectingIds = AllConnectedIds();
		return connectingIds.Where(conId => !exceptConnectionIds.Contains(conId)).ToArray();
	}
	
	private string PlayerIdFromConnectionId (string connectionId) {
		var index = Array.FindIndex(connections, connection => connection.connectionId == connectionId);
		if (-1 < index) return connections[index].playerId;
		return string.Empty;
	}
	
	private string ConnectionIdFromPlayerId (string playerId) {
		var index = Array.FindIndex(connections, connection => connection.playerId == playerId);
		if (-1 < index) return connections[index].connectionId;
		return string.Empty;
	}
}

public class World {
	public readonly string worldId;
	private List<PlayerInServer> playersInServer;
	
	public World () {
		this.worldId = Guid.NewGuid().ToString();
		this.playersInServer = new List<PlayerInServer>();
	}
	
	public void AddPlayer (PlayerInServer player) {
		playersInServer.Add(player);
	}
	
	public List<Commands.PlayerIdAndPos> PlayersInfos () {
		var playerInfos = new List<Commands.PlayerIdAndPos>();
		foreach (var playerInServer in playersInServer) {
			var playerId = playerInServer.playerId;
			var pos = playerInServer.pos;
			var dir = playerInServer.dir;
			playerInfos.Add(new Commands.PlayerIdAndPos(playerId, pos, dir));
		}
		return playerInfos;
	}
}

public class PlayerInServer {
	public readonly string playerId;
	
	public Commands.StructVector3 pos;
	
	public DirectionEnum dir;
	
	public readonly bool isDummy;
	
	public PlayerInServer (string playerId, int x, int z, int height, DirectionEnum dir, bool isDummy=false) {
		this.playerId = playerId;
		this.pos = new Commands.StructVector3(x, z, height);
		this.dir = dir;
		
		this.isDummy = isDummy;
	}
}