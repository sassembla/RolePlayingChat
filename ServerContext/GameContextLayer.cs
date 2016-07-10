using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Automatine;
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
	
	
	
	
	private Queue<DataPack> gameDataQueue = new Queue<DataPack>();
	
	private readonly string gameLayerId;
	
	private BattleState state = BattleState.STATE_READY;
	
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
			
			// count the number of data to target player.
			if (datas.Count == 1) {
				Publish(connectionId, datas[0].ToData());
				continue;
			}
			
			// publish multiple data as combined 1 data.
			var byteDatas = stackedData[connectionId].Select(command => command.ToData()).ToArray();
			var combinedData = new Commands.PackedDatas(playerId, byteDatas).ToData();
			
			Publish(connectionId, combinedData);
		}
		
		// clear list.
		stackedData.Clear();
	}
	
	
	
	public GameContextLayer (List<string> reservedPlayerIds, Action<string, byte[]> publish) {
		gameLayerId = Guid.NewGuid().ToString();
		Action<Commands.BaseData, string[]> SendById = (Commands.BaseData command, string[] playerIds) => {
			var connectionIds = playerIds.Select(i => ConnectionIdFromPlayerId(i)).ToArray();
			StackPublish(command, connectionIds);
		};
		world = new World(SendById);
		
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
		
		lock (gameDataQueue) gameDataQueue.Enqueue(new DataPack(playerId, data));
	}
	
	public void EnqueOnDisconnect (string playerId, byte[] data) {
		lock (gameDataQueue) gameDataQueue.Enqueue(new DataPack(playerId, data));
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
				// worldにいるユーザー = dummyの挙動を開始する。

				world.UpdateWorld(gameFrame, StackPublish);
				
				UpdateXrossPeer(gameFrame);
				
				
				
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
		lock (gameDataQueue) {
			while (gameDataQueue.Any()) {
				var dataPack = gameDataQueue.Dequeue();
				InputToXrossPeer(gameFrame, dataPack.playerId, dataPack.data);
			}
		};
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
					var pos = new Commands.StructVector3(0, 0, 30);
					// 適当な位置をでっち上げる
					var newPlayer = new PlayerContext(onConnectedPlayerId, pos, DirectionEnum.East);
					world.AddPlayer(newPlayer);
					StackPublish(new Commands.EntriedId(onConnectedPlayerId, pos, newPlayer.forward), AllConnectedIds());
				}
				

				/*
					ダミーを10人くらい降らせよう。
				*/
				if (AllConnectedIds().Length == 1) {
					XrossPeer.Log("最初の接続者 onConnectedPlayerId:" + onConnectedPlayerId + " が来たので、ダミーを降らせる。ランダムシードがすげえーー怪しい時があるな。");

					var dummyCount = 2;

					var xRand = new byte[dummyCount];
					var zRand = new byte[dummyCount];
					
					var r1 = new System.Random();
					r1.NextBytes(xRand);
					
					var r2 = new System.Random();
					r2.NextBytes(zRand);
					
					 
					{
						var dummyPlayerId = Guid.NewGuid().ToString();
						var dir = DirectionEnum.South;
						var pos = new Commands.StructVector3(10, 10, 30);
						var dummyPlayer = new PlayerContext(dummyPlayerId, pos, dir);
						dummyPlayer.isDummy = true;
						world.AddPlayer(dummyPlayer);
					}
					
					{
						var dummyPlayerId = Guid.NewGuid().ToString();
						var dir = DirectionEnum.South;
						var pos = new Commands.StructVector3(10, 0, 30);
						var dummyPlayer = new PlayerContext(dummyPlayerId, pos, dir);
						dummyPlayer.isDummy = true;
						world.AddPlayer(dummyPlayer);
					}
				}
				
				/*
					receive world info.
				*/
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

				
				if (!world.IsDummyPlayer(walkingPlayerId)) {
					var playerInfo = world.GetPlayerInfo(walkingPlayerId);
					playerInfo.auto = new Walk<PlayerContext, List<PlayerContext>>(gameFrame, playerInfo);
				}
				return;
			}
			case Commands.CommandEnum.Messaging: {
				var messageData = Commands.FromData<Commands.Messaging>(data);
				var senderPlayerId = messageData.playerId;
				var targetPlayerId = messageData.targetPlayerId;
				var message = messageData.message;
				
				// サーバまで届いているので、そのことの反射をする。
				var senderConnectionId = ConnectionIdFromPlayerId(senderPlayerId);
				StackPublish(new Commands.Messaging(senderPlayerId, senderPlayerId, "我:" + senderPlayerId + ":" + message), new string[]{senderConnectionId});
				
				XrossPeer.Log("message:" + message + " from playerId:" + senderPlayerId + " to targetPlayerId:" + targetPlayerId);
				
				/*
					is bot, message turns to "order".
				*/
				if (world.IsDummyPlayer(targetPlayerId)) {
					GenerateAnswer(targetPlayerId, senderPlayerId, message, senderConnectionId);
					return;
				}
				
				var connectionId = ConnectionIdFromPlayerId(targetPlayerId);
				StackPublish(new Commands.Messaging(senderPlayerId, targetPlayerId, "相手:" + senderPlayerId + ":" + message), new string[]{connectionId});
				return;
			}
			default: {
				XrossPeer.Log("gameContextLayer unhandled command:" + command);
				break;
			}
		}
	}

	/**
		文字列のパターンによって、AIにお願いをすることができる。
	*/
	private void GenerateAnswer (string dummyPlayerId, string senderPlayerId, string message, string senderConnectionId) {
		XrossPeer.Log("この時点で、このAIが忙しくなければ、っていう判断をしてもいいかもしれない。localとremoteのAutoのあり方をどうするかな。");
		if (!message.EndsWith("?")) { 
			StackPublish(new Commands.Messaging(dummyPlayerId, senderPlayerId, "村人:" + dummyPlayerId + ":" + message + " って何？"), new string[]{senderConnectionId});
			return;
		}

		// ?で終わってる場合、Queryとみて、動作開始。

		// 今回は問答なしで、追いかけて行って云々っていう感じにする。まずカメラ引いてしまおう。

		var ourIds = new List<string>{dummyPlayerId, senderPlayerId};
		var anotherTargetId = world.ExceptPlayerIds(ourIds)[0];
		StackPublish(new Commands.Messaging(dummyPlayerId, senderPlayerId, "村人_" + dummyPlayerId + ":" + "お、わかった〜、" + anotherTargetId + "に、\"" + message.Substring(0, message.Length - 1) + "\" って伝えとく。"), new string[]{senderConnectionId});
		
		// メッセージを保持、実際にターゲットに向かって歩いてく。近づいて行って、最終的にメッセージを伝える。
		var reservedMessage = "あのね〜 " + senderPlayerId + "からの伝言で、" + "\"" + message + "\"" + "ってさ。";
		XrossPeer.Log("reservedMessage:" + reservedMessage);

		// サーバ側でAutoをどうやって組もうかな。
		/*
			・サーバ側でのAutoを持つ
			・Autoを初期化する
			・Autoを適当にスタックする(デフォルト状態もAutoとして持った方がいいのかな。)
			・Autoからの発信を行う
			とかか。全部協調動作する必要ないんで、サーバ側で適当なフレームで動かしつつ、時間がきたら実行、っていう感じかな。

			Queryの部分をAutoで積む？
			・何時頃 とか
			・何回 とか
			・どのくらいしつこく とか
		*/
		world.StackAutoName(dummyPlayerId, "DoStalk", new List<string>{anotherTargetId, reservedMessage});// 付け回して、相手にメッセージ投げて、それが済んだら
		world.StackAutoName(dummyPlayerId, "DoNotify", new List<string>{senderPlayerId, anotherTargetId, reservedMessage});// 終わったよーって通知を出す
		world.StackAutoName(dummyPlayerId, "DoStalk", new List<string>{senderPlayerId, reservedMessage});// メッセージを渡したやつに会いに行く
		
		var playerContext = world.GetPlayerInfo(dummyPlayerId);
		var newAuto = new DoOrder<PlayerContext, List<PlayerContext>>(gameFrame, playerContext);
		world.SetAuto(dummyPlayerId, newAuto, gameFrame);
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
	private readonly Action<Commands.BaseData, string[]> StackPublish;

	public readonly string worldId;
	private List<PlayerContext> playerContextsInServer;
	
	public World (Action<Commands.BaseData, string[]> StackPublish) {
		this.worldId = Guid.NewGuid().ToString();
		this.playerContextsInServer = new List<PlayerContext>();
		this.StackPublish = StackPublish;
	}
	
	public void AddPlayer (PlayerContext player) {
		playerContextsInServer.Add(player);
	}
	
	public List<string> ExceptPlayerIds (List<string> exceptPlayerIds) {
		return playerContextsInServer.Where(p => !exceptPlayerIds.Contains(p.playerId)).Select(p => p.playerId).ToList();
	}

	public List<Commands.PlayerIdAndPos> PlayersInfos () {
		var playerInfos = new List<Commands.PlayerIdAndPos>();
		foreach (var playerContext in playerContextsInServer) {
			var playerId = playerContext.playerId;
			var pos = playerContext.Position();
			var dir = playerContext.forward;
			playerInfos.Add(new Commands.PlayerIdAndPos(playerId, pos, dir));
		}
		return playerInfos;
	}
	
	public bool IsDummyPlayer (string playerId) {
		var playerInServer = playerContextsInServer.Where(p => p.playerId == playerId).FirstOrDefault();
		return playerInServer.isDummy;
	}

	public void UpdateWorld (int frame, Action<Commands.BaseData, string[]> pub) {
		foreach (var player in playerContextsInServer) {
			if (player.auto == null) continue;

			if (!player.isDummy) {
				if (player.auto.ShouldFalldown(frame)) {
					continue;
				}
			} else {
				
				// dummy.
				if (player.auto.ShouldFalldown(frame)) {
					if (player.stackedDummyAutos.Count == 0) continue;
					var stackedAutoInfo = player.stackedDummyAutos[0];
					player.stackedDummyAutos.RemoveAt(0);

					// stackedAutoName
					XrossPeer.Log("next stackedAutoName:" + stackedAutoInfo.autoName);
					switch (stackedAutoInfo.autoName) {
						case "DoStalk": {
							player.dummyTargetId = stackedAutoInfo.parameters[0];
							player.dummyMessage = stackedAutoInfo.parameters[1];
							player.auto = player.auto.ChangeTo(new DoStalk<PlayerContext, List<PlayerContext>>(frame, player));
							break;
						}
						default: {
							XrossPeer.Log("未定義の状態:" + stackedAutoInfo.autoName);
							break;
						}
					}
				}
			}

			player.auto.Update(frame, playerContextsInServer);

			if (player.stackedCommands.Any()) {
				var allPlayerIds = playerContextsInServer.Where(p => !p.isDummy).Select(p => p.playerId).ToArray();

				foreach (var stackedCommand in player.stackedCommands) {
					StackPublish(stackedCommand, allPlayerIds);
				}
				player.stackedCommands.Clear();
			} 
		}
	}

    public void SetAuto (string playerId, Auto<PlayerContext, List<PlayerContext>> newAuto, int frame) {
        var playerContext = GetPlayerInfo(playerId);
		if (playerContext == null) {
			XrossPeer.Log("対象のplayer:" + playerId + " nullだったのでstackに失敗");
			return;
		}
		
		playerContext.auto = newAuto;
    }

	public void StackAutoName (string playerId, string autoName, List<string> parameters) {
		var playerContext = GetPlayerInfo(playerId);
		if (playerContext == null) {
			XrossPeer.Log("対象のplayer:" + playerId + " nullだったのでstackに失敗");
			return;
		}
		
		playerContext.stackedDummyAutos.Add(new AutoInfo(autoName, parameters));
	}

	public PlayerContext GetPlayerInfo (string playerId) {
		return playerContextsInServer.Where(p => p.playerId == playerId).FirstOrDefault();
	}
}