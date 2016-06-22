using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using WebuSocketCore;

public class OnExecute : MonoBehaviour {
	
	public string playerId;// 100 ~ 199の間でランダムにしよう。
	
	public List<PlayerContext> players;
	
	public Dictionary<string, GameObject> playerModels;
	
	/*
		メッセージをどうやってデザインしようかな。
		✔︎まず他人が必要だな。ダミー出そう。
		✔他人の近所にいったら、会話ボタン？どうやってチャット開始しよう。
		✔突然ウィンドウでるんでいいや。状態としては？
		✔walk -> 円形近接 -> 歩き終わったタイミングでTalk? Talk終わったらDefault。

		ToDoのためのAPIを組もう。
		へいらっしゃい！から始まる問答の入力
	*/
	
	private GameObject uiScreen;
	
	private WebuSocket webuSocket;

	private Queue<byte[]> binaryQueue = new Queue<byte[]>();
	
	// Use this for initialization
	void Start () {
		uiScreen = GameObject.Find("EasyTouchControlsCanvas") as GameObject;
		
		players = new List<PlayerContext>();
		playerModels = new Dictionary<string, GameObject>();
		
		var dateMilliSec = DateTime.Now.Millisecond;
		UnityEngine.Random.seed = dateMilliSec;
		playerId = UnityEngine.Random.Range(100, 199).ToString();
		Debug.LogError("playerId:" + playerId);
		
		
		var keySetting = (StandardAssetsConnectorSettings)ScriptableObject.CreateInstance("StandardAssetsConnectorSettings");
		var WEBSOCKET_ENTRYPOINT = keySetting.DomainKey() + keySetting.ClientKey();

		/*
			ProcessDataをMainThreadで呼ぶためにセットしている
		*/
		Observable.EveryUpdate().Subscribe(
			_ => {
				lock (binaryQueue) {
					if (0 == binaryQueue.Count) return;  
					while (0 < binaryQueue.Count) ProcessData(binaryQueue.Dequeue());
					binaryQueue.Clear();
				}
			}
		);
		
		webuSocket = new WebuSocket(
			WEBSOCKET_ENTRYPOINT,
			1024 * 100,
			() => {
				MainThreadDispatcher.Post(
					(b) => {
						Debug.LogError("connected.");
					},
					this
				);
			}, 
			(Queue<ArraySegment<byte>> datas) => {
				// enqueue datas to local queue.
				lock (binaryQueue) {
					while (0 < datas.Count) {
						var data = datas.Dequeue();
						var bytes = new byte[data.Count];
						Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);
						binaryQueue.Enqueue(bytes);
					}
				}
			}, 
			() => {
				Debug.LogError("pingされたぞ〜");
			}, 
			closeReason => {
				Debug.LogError("closeReason:" + closeReason);
				
				MainThreadDispatcher.Post(
					(b) => {
						// run on main thread.
					},
					this
				);
			}, 
			(errorReason, e) => {
				Debug.LogError("errorReason:" + errorReason);

				MainThreadDispatcher.Post(
					(b) => {
						// run on main thread.
					},
					this
				);
			}, 
			new Dictionary<string, string>{
				{"playerId", playerId}
			}
		);
	}
	
	
	private void ProcessData (byte[] data) {
		var commandAndPlayerId = Commands.ReadCommandAndSourceId(data);
		var command = commandAndPlayerId.command;
		var commandSourcePlayerId = commandAndPlayerId.playerId;
		
		/*
			複数コマンドを固めたものを受け取った場合、展開して実行
		*/
		if (command == Commands.CommandEnum.Datas) {
			var datas = Commands.FromData<Commands.PackedDatas>(data);
			for (var i = 0; i < datas.datas.Length; i++) {
				var containedByteData = datas.datas[i].data;
				var commandAndPlayerId2 = Commands.ReadCommandAndSourceId(containedByteData);
				var currentPlayerId = commandAndPlayerId2.playerId;
				var currentCommand = commandAndPlayerId2.command;
				
				ExecuteCommandFromBytes(currentPlayerId, currentCommand, containedByteData);
			}
			return;
		}
		
		/*
			それ以外のコマンドであれば、単に実行。
		*/
		ExecuteCommandFromBytes(commandSourcePlayerId, command, data);
	}
	
	private PlayerContext NewPlayerContext (string playerId, Commands.StructVector3 pos, DirectionEnum dir) {
		var playerContext = new PlayerContext(playerId, pos, dir);		
		var auto = new Spawning<PlayerContext, List<PlayerContext>>(clientFrame, playerContext);
		
		playerContext.auto = auto;
		
		var prefab = Resources.Load("Chara") as GameObject;
		playerModels[playerId] = Instantiate(prefab, new Vector3(playerContext.x, playerContext.height, playerContext.z), Quaternion.identity) as GameObject;
		return playerContext;
	}
	
	private void ExecuteCommandFromBytes (string commandSourcePlayerId, Commands.CommandEnum command, byte[] data) {
		
		switch (command) {
			case Commands.CommandEnum.EntriedId: {
				var entriedData = Commands.FromData<Commands.EntriedId>(data);
				
				// 新規参加ユーザーを生成
				{
					var entriedPlayerId = entriedData.playerId;
					var pos = entriedData.pos;
					var dir = entriedData.dir;
					
					// Debug.LogError("EntriedIdをサーバから受け取った entriedPlayerId:" + entriedPlayerId);
					
					var playerContext = NewPlayerContext(entriedPlayerId, pos, dir);
					players.Add(playerContext);
					
					/*
						カメラセッティング
					*/
					if (entriedPlayerId == this.playerId) {
						var cinemachineCamera = GameObject.Find("CinemachineVirtualCamera") as GameObject;
						var cinemachineComponent = cinemachineCamera.GetComponent<CinemachineVirtualCamera>() as CinemachineVirtualCamera;
						cinemachineComponent.CameraTransposerTarget = playerModels[entriedPlayerId].transform;
					} 
				}
				
				if (commandSourcePlayerId == this.playerId) {
					Debug.LogError("自分がエントリーしたのでSpawnRequestを送る");
					StackPublish(new Commands.SpawnRequest(playerId));
				}
				return;
			}
			
			case Commands.CommandEnum.Spawn: {
				var spawnData = Commands.FromData<Commands.Spawn>(data);
				var spawnPlayerId = spawnData.playerId;
				
				// Debug.LogError("誰かspawnしたんで、Autoの状態を変更する。自分以外のspawnって意味あるのかな、、あー、入室とかか。spawnPlayerId:" + spawnPlayerId);
				var targetPlayerContext = ChoosePlayerContext(spawnPlayerId);
				targetPlayerContext.auto = targetPlayerContext.auto.ChangeTo(new Default<PlayerContext, List<PlayerContext>>(clientFrame, targetPlayerContext));
				return;
			}
			
			case Commands.CommandEnum.WorldData: {
				var worldData = Commands.FromData<Commands.WorldData>(data);
				
				var currentPlayersIds = players.Select(p => p.playerId).ToArray();
				foreach (var playerData in worldData.players) {
					if (playerData.playerId == this.playerId) continue;
					if (currentPlayersIds.Contains(playerData.playerId)) continue;
					
					var playerId = playerData.playerId;
					var playerPos = playerData.pos;
					var playerDir = playerData.dir;
					
					var playerContext = NewPlayerContext(playerId, playerPos, playerDir);
					
					Debug.LogError("とりあえず適当に、default状態にしておく。特に問題ないはず。");
					playerContext.auto = new Default<PlayerContext, List<PlayerContext>>(clientFrame, playerContext);
					
					players.Add(playerContext);
				}
				return;
			}
			
			case Commands.CommandEnum.Walk: {
				var walkData = Commands.FromData<Commands.Walk>(data);
				var walkingPlayerId = walkData.playerId;
				var walkingDir = walkData.direction;
				var walkBasePos = walkData.pos;
				
				if (walkingPlayerId == this.playerId) {
					// ignore.
					// Debug.LogError("自分が歩いてる");
					return;
				}
				
				var playerContext = ChoosePlayerContext(walkingPlayerId);
				playerContext.x = walkBasePos.x;
				playerContext.z = walkBasePos.z;
				playerContext.height = walkBasePos.height;
				playerContext.forward = walkingDir;
				
				playerContext.auto = new Walk<PlayerContext, List<PlayerContext>>(clientFrame, playerContext);
				return;
			}
			case Commands.CommandEnum.ForceMove: {
				var forceMoveData = Commands.FromData<Commands.ForceMove>(data);
				var movingPlayerId = forceMoveData.playerId;
				var movingPlayerDir = forceMoveData.direction;
				var movingPlayerPos = forceMoveData.pos;

				// サーバ側でプレイヤー位置とかどうなってんだろ、それに合わせるチャンスがあるはず。
				Debug.LogError("ForceMove きました");

				// 係数系が異なる。そのままマッピングしてもダメだな＝＝
				// var playerContext = ChoosePlayerContext(movingPlayerId);
				// playerContext.x = movingPlayerPos.x;
				// playerContext.z = movingPlayerPos.z;
				// playerContext.height = 0;
				// playerContext.forward = movingPlayerDir;
				return;
			}
			case Commands.CommandEnum.Messaging: {
				var messageData = Commands.FromData<Commands.Messaging>(data);
				var messageTargetPlayer = messageData.targetPlayerId;
				var messageSender = messageData.playerId;
				
				/*
					自分宛だったら、表示する。
					GUIつくるところが重いな、、最初から生成しとくか、、
				*/
				if (messageTargetPlayer == this.playerId) {
					var message = messageData.message;
					if (windowInstance == null) {
						StartTalking();
					}
					textView.text = textView.text + "\n\n" + message;
				}
				
				/*
					強制的に、会話をしてきた人との会話になってる。
					現在会話している人がすでに存在する場合、そのへんを加味して吹き出し化とかするかな。
				*/
				var myContext = ChoosePlayerContext(this.playerId);
				if (myContext.auto.Contains(AutoConditions.Talkable.Receivable)) {
					Debug.LogError("receiver(me)'s talkingPlayerIdd:" + myContext.talkingPlayerId);
					// マッチしなかったら、だれか別の人と喋ってる。
					
					myContext.talkingPlayerId = messageSender;
					myContext.auto = new Talk<PlayerContext, List<PlayerContext>>(clientFrame, myContext);
				} 
				return;
			}
			
			/*
				unhandled
			*/			
			default: {
				Debug.LogError("client unhandled command:" + command);
				return;
			}
		}
	}
	
	int clientFrame = 0;
	
	// Update is called once per frame
	void Update () {
		/*
			・視界に入ってる範囲だけが対象、とかのリミテーションが必要
			・移動のリミテーションせねば。
			・影落ちないと落下位置わかんねーな
		*/
		
		/*
			update players.
		*/
		foreach (var playerContext in players) {
			UpdatePlayerContext(playerContext);
			
			/*
				dir, posの同期
			*/
			
			playerModels[playerContext.playerId].transform.position = new Vector3(playerContext.x * RolePlayingChatDefinitions.FloorUnit, 0, playerContext.z * RolePlayingChatDefinitions.FloorUnit);
			var targetAngle = new Vector3(0, 90 * ((int)playerContext.forward - 1), 0);
			playerModels[playerContext.playerId].transform.GetChild(0).transform.eulerAngles = targetAngle;
			
			if (playerModels[playerContext.playerId].transform.position.y < 0) playerModels[playerContext.playerId].transform.position = new Vector3(playerModels[playerContext.playerId].transform.position.x, 0, playerModels[playerContext.playerId].transform.position.z);  
			
			if (!string.IsNullOrEmpty(playerContext.motionName)) {
				// SetMotion();// いつかやる。
				// Debug.LogError("playerModels[playerContext.playerId]:" + playerModels[playerContext.playerId]);
				playerContext.motionName = string.Empty;
			}
		}
		
		PublishStackedData();
		
		clientFrame++;// そのうちフローすると思う。
	}
	
	private void UpdatePlayerContext (PlayerContext context) {
		if (context.playerId == this.playerId) {
			// 近所にだれかいて、そいつが通話可能だったら、会話ウィンドウを出す。
			// 最後に会話したやつとは話をしないほうがいいのかな、離れ損なって連続、、ていうのがありそう。
			// どうやって状態管理しよう、、ああ、lastTalkedPlayerIdでいいのか。
			// walkの時に、一番近くにいるプレイヤーのIdを保持しとけばいいな。
			
			/*
				話しかける、話しかけられる、の関係をどう整理しようかな。
				声をかけることができる = Emittable
				声をうけることができる = Receivable
				双方がないと成立しないんだよな。
			*/
			
			if (!string.IsNullOrEmpty(context.talkablePlayerId) && context.talkablePlayerId != context.lastTalkedPlayerId) {
				var targetPlayerId = context.talkablePlayerId;
				if (context.auto.Contains(AutoConditions.TalkEmittable.Emittable)) {
					Debug.LogError("自分のほうは話しかけることができる");
					
					var talkTargetContext = ChoosePlayerContext(targetPlayerId);
					if (talkTargetContext.auto.Contains(AutoConditions.Talkable.Receivable)) {
						Debug.LogError("相手のほうも話を受けることができる。この辺は、会話対象としてOn/Offとかも視野に入る気がする。sendを許すだけ。");
						Debug.LogError("エンカウント、一方的に攻撃?ができる。相手側は別に返答しなければTalkingに入らない。");
						context.talkingPlayerId = targetPlayerId;
						context.auto = new Talk<PlayerContext, List<PlayerContext>>(clientFrame, context);
						
						StartTalking(this.playerId, targetPlayerId);
					} else {
						Debug.LogError("話しかけられなかったよ、、、対象のautoは？ talkTargetContext.auto.autoName:" + talkTargetContext.auto.autoName);
					}
					
				}
			}
		}
		
		/*
			誰かから話しかけられていれば、ここで判断できる。
		*/
		var stackeds = context.auto.StackedChangers();
		if (stackeds.Any()) {
			Debug.LogError("stackeds:" + stackeds.Count);
			
			
			// これ、ノーティフケーションに使えるな。
			// /*
			// 	loadingからのみ、talkに遷移できるんじゃないかっていう。
			// */
			// if (context.auto.Contains(AutoConditions.Talk.Loading)) { 
			// 	foreach (var stacked in stackeds) {
			// 		var stackedName = stacked.ChangerName();
					
			// 		switch (stackedName) {
			// 			case "TalkChanger": {
			// 				context.auto = new Talk<PlayerContext, List<PlayerContext>>(clientFrame, context);
			// 				break;
			// 			}
			// 			default: {
			// 				Debug.LogError("なんか違うのきたぞ。:" + stackedName);
			// 				break;
			// 			}
			// 		}
			// 	}
			// }
		}
		
		/*
			デフォルト落ち
		*/
		if (context.auto.ShouldFalldown(clientFrame)) {
			// これも一種のイベントだな。
			
			// このプレイヤーのこのAuto終わってるので、デフォルトに戻す。
			
			context.auto = new Default<PlayerContext, List<PlayerContext>>(clientFrame, context);
		}
		
		/*
			自分のキャラの場合、操作を受け付ける。
		*/
		if (context.playerId == this.playerId) {
			ExecuteMyPlayer(context);
		}
		
		context.auto.Update(clientFrame, players);
	}
	
	private string inputMessage;
	
	public void InputMessage (string wholeMessage) {
		inputMessage = wholeMessage;
	}
	
	public void SendMessage () {
		if (string.IsNullOrEmpty(inputMessage)) return;
		
		Debug.LogError("SendMessage inputMessage:" + inputMessage);
		var playerContext = ChoosePlayerContext(this.playerId);
		playerContext.messageSend = inputMessage;
		
		inputKey = KeyEnum.Send;
	}
	
	public void InputDamaged () {
		Debug.LogError("InputDamaged");
		var playerContext = ChoosePlayerContext(this.playerId);
		// playerContext.auto.StackChanger();// stackするだけにしとく。Changer作る素晴らしい口実ができた。
	}
	
	public void InputHealed () {
		Debug.LogError("InputHealed");
		var playerContext = ChoosePlayerContext(this.playerId);
		// playerContext.auto.StackChanger();
	}
	
	private KeyEnum inputKey;
	private DirectionEnum inputDirection = DirectionEnum.None;
	
	private void ExecuteMyPlayer (PlayerContext context) {
		if (context.auto.Contains(AutoConditions.Talk.Talking)) {
			if (inputKey != KeyEnum.None) {
				switch (inputKey) {
					case KeyEnum.Send: {
						var targetPlayerId = context.talkingPlayerId;
						Debug.LogError("talk targetPlayerId:" + targetPlayerId);
						StackPublish(new Commands.Messaging(this.playerId, targetPlayerId, context.messageSend));
						context.messageSend = string.Empty;
						
						inputKey = KeyEnum.None;
						break;
					}
					default: {
						Debug.LogError("まだ対処してない会話中の何か:" + inputKey);
						break;
					}
				}
			} 
		} 
		
		
		// 方向キー操作受付
		if (inputDirection != DirectionEnum.None) {
			
			// あー、、このへんswitchで書けるといいなあ。もしくはchangerか。
			
			
			/*
				会話中に移動したら、会話キャンセルしたいよね。
			*/
			if (context.auto.Contains(AutoConditions.Talk.Talking)) {
				// 会話を途切れさせる。っていうか歩く。
				EndTalking();
				
				// 会話した相手の情報の保持 あんまり厳格にやらなくていい感じがした。
				// context.lastTalkedPlayerId = context.talkingPlayerId;
				// context.talkingPlayerId = string.Empty;
				// context.messageSend = string.Empty;
				
				context.forward = inputDirection;
				context.auto = context.auto.ChangeTo(new Walk<PlayerContext, List<PlayerContext>>(clientFrame, context));
				StackPublish(new Commands.Walk(this.playerId, inputDirection, new Commands.StructVector3((int)context.x, (int)context.z, (int)context.height)));
			}
			
			if (context.auto.Contains(AutoConditions.Control.Contorllable)) {
				context.forward = inputDirection;
				context.auto = context.auto.ChangeTo(new Walk<PlayerContext, List<PlayerContext>>(clientFrame, context));
				StackPublish(new Commands.Walk(this.playerId, inputDirection, new Commands.StructVector3((int)context.x, (int)context.z, (int)context.height)));
			}
			
			// consume.
			inputDirection = DirectionEnum.None;
		}
	}
	
	// private D.Stopwatch sw;
	
	GameObject windowInstance;
	Text textView;
	
	public void StartTalking (params string[] playerIds) {
		// // ２者間の中間位置にオブジェクトをおく。向きを設定することでカメラの調整ができると思う。Composerの調整でどうやればいいんだろう。
		// var centeringObject =
		
		//  会話ウィンドウを出す。
		var windowPrefab = Resources.Load("TalkWindow") as GameObject;
		windowInstance = Instantiate(windowPrefab);
		
		windowInstance.transform.SetParent(uiScreen.transform, false);
		GameObject.Find("InputField").GetComponent<InputField>().onValueChanged.AddListener((string s) => InputMessage(s));
		GameObject.Find("Send").GetComponent<Button>().onClick.AddListener(() => SendMessage());
		GameObject.Find("Damaged").GetComponent<Button>().onClick.AddListener(() => InputDamaged());
		GameObject.Find("Healed").GetComponent<Button>().onClick.AddListener(() => InputHealed());
		textView = GameObject.Find("MessageText").GetComponent<Text>();
	}
	
	public void EndTalking () {
		Destroy(windowInstance);
	}
	
	
	
	private PlayerContext ChoosePlayerContext (string playerId) {
		return players.Where(p => p.playerId == playerId).FirstOrDefault();
	}
	
	
	public void OnJoystickInput (Vector2 dir) {
		var degree = Math.Abs(Math.Atan2(dir.x, dir.y) * 180.0 / Math.PI);
		
		// convert 0 ~ 360 degree.
		if (dir.x < 0) degree = (180.0 * 2) - degree;
		
		if (degree < (360.0 * 1f/8f)) inputDirection = DirectionEnum.North;
		else if (degree < (360.0 * 3f/8f)) inputDirection = DirectionEnum.East;
		else if (degree < (360.0 * 5f/8f)) inputDirection = DirectionEnum.South;
		else if (degree < (360.0 * 7f/8f)) inputDirection = DirectionEnum.West;
		else inputDirection = DirectionEnum.North;
	}
	
	
	
	
	
	private List<Commands.BaseData> stackedCommands = new List<Commands.BaseData>();
	
	private void StackPublish (Commands.BaseData data) {
		stackedCommands.Add(data);
	}
	
	private void PublishStackedData () {
		if (!stackedCommands.Any()) return;
		 
		foreach (var command in stackedCommands) {
			webuSocket.Send(command.ToData());
		}
		stackedCommands.Clear();
	}
	
	
	public void OnApplicationQuit () {
		webuSocket.Disconnect(true);
	}
}
