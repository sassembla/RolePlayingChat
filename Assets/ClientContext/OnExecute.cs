using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cinemachine;
using UnityEngine;
using WebSocketControl;

public class OnExecute : MonoBehaviour {
	
	public string playerId;// 100 ~ 199の間でランダムにしよう。
	
	public List<PlayerContext> players;
	
	public Dictionary<string, GameObject> playerModels;
	
	/*
		メッセージをどうやってデザインしようかな。
		✔︎まず他人が必要だな。ダミー出そう。
		・他人の近所にいったら、会話ボタン？どうやってチャット開始しよう。
		・突然ウィンドウでるんでいいや。状態としては？
		・walk -> 円形近接 -> 歩き終わったタイミングでTalk? Talk終わったらDefault。
		・
	*/
	
	private GameObject uiScreen;
	
	
	// Use this for initialization
	void Start () {
		uiScreen = GameObject.Find("EasyTouchControlsCanvas") as GameObject;
		
		players = new List<PlayerContext>();
		playerModels = new Dictionary<string, GameObject>();
		
		var dateMilliSec = DateTime.Now.Millisecond;
		UnityEngine.Random.seed = dateMilliSec;
		playerId = UnityEngine.Random.Range(100, 199).ToString();
		Debug.LogError("playerId:" + playerId);
		
		// クライアント接続を開始して、接続できたら云々。これstaticな必要あるんかな、、まあWebuSocketをそのまま使うよりは楽だな。
		WebSocketConnectionController.InitWebSocketConnection(
			new Dictionary<string, string>{
				{"playerId", playerId}
			}, 
			"rolePlayAgent", // string agent,
			() => {
				Debug.LogError("connected!");
			}, 
			(List<byte[]> datas) => {
				foreach (var data in datas) ProcessData(data);
			},
			(connectionFailedReason) => {
				Debug.LogError("connection failed, connectionFailedReason:" + connectionFailedReason);
			}, 
			(disconnectedReason) => {
				Debug.LogError("disconnected, reason:" + disconnectedReason);
			},
			false,
			() => {}
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
			var datas = Commands.FromData<Commands.Datas>(data);
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
	
	private void ExecuteCommandFromBytes (string commandSourcePlayerId, Commands.CommandEnum command, byte[] data) {
		
		switch (command) {
			case Commands.CommandEnum.EntriedId: {
				var entriedData = Commands.FromData<Commands.EntriedId>(data);
				
				// 新規参加ユーザーを生成
				{
					var entriedPlayerId = entriedData.playerId;
					var pos = entriedData.pos;
					
					// Debug.LogError("EntriedIdをサーバから受け取った entriedPlayerId:" + entriedPlayerId);
					
					var playerContext = new PlayerContext(entriedPlayerId, pos);
					var auto = new Spawning<PlayerContext, List<PlayerContext>>(clientFrame, playerContext);
					
					playerContext.auto = auto;
					players.Add(playerContext);
					
					var prefab = Resources.Load("Chara") as GameObject;
					playerModels[entriedPlayerId] = Instantiate(prefab, new Vector3(playerContext.x, playerContext.height, playerContext.z), Quaternion.identity) as GameObject;
					
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
				
				players
					.Where(p => p.playerId == spawnPlayerId)
					.Select(p => p.auto = p.auto.ChangeTo(new Default<PlayerContext, List<PlayerContext>>(clientFrame, p))).ToArray();
				return;
			}
			
			case Commands.CommandEnum.Message: {
				try {
					var messageData = Commands.FromData<Commands.Message>(data);
					var message = messageData.message;
					Debug.Log("message from server:" + message);
				} catch (Exception e) {
					Debug.LogError("e:" + e);
					for (var i = 0; i < data.Length; i++) {
						Debug.LogError("e:" + i + " data:" + data[i]);
					}
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
	
	private List<Commands.BaseData> stackedCommands = new List<Commands.BaseData>();
	
	private void StackPublish (Commands.BaseData data) {
		stackedCommands.Add(data);
	}
	
	private void PublishStackedData () {
		if (!stackedCommands.Any()) return;
		 
		foreach (var command in stackedCommands) {
			WebSocketConnectionController.SendCommandAsync(command.ToData());
		}
		stackedCommands.Clear();
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
			
			// キャラの位置とか向きをContextから取得、反映させる。
			// 対象のgameObjectに対して、パラメータを反映させる。
			/*
				dir, pos, 
			*/
			playerModels[playerContext.playerId].transform.position = new Vector3(playerContext.x, playerModels[playerContext.playerId].transform.position.y, playerContext.z);
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
				if (context.auto.Contains(AutoConditions.Talkable.Emittable)) {
					Debug.LogError("自分のほうは話しかけることができる");
					
					var talkTargetContext = players.Where(p => p.playerId == targetPlayerId).FirstOrDefault();
					if (talkTargetContext.auto.Contains(AutoConditions.Talkable.Receivable)) {
						Debug.LogError("相手のほうも話を受けることができる。この辺は、会話対象としてOn/Offとかも視野に入る気がする。sendを許すだけ。");
						Debug.LogError("エンカウント、一方的に攻撃?ができる。相手側は別に返答しなければTalkingに入らない。");
						context.auto = new Talk<PlayerContext, List<PlayerContext>>(clientFrame, context);
						
						StartTalking(this.playerId, targetPlayerId);
					} else {
						Debug.LogError("話しかけられなかったよ、、、");
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
	
	private KeyEnum inputKey;
	private DirectionEnum inputDirection = DirectionEnum.None;
	
	private void ExecuteMyPlayer (PlayerContext context) {
		if (context.auto.Contains(AutoConditions.Talk.Talking)) {
			// 対象と会話中なんで、会話ウィンドウと入力ウィンドウがあるはず。
			
			
			// カメラを2人の中間点に移動、とか？Composerを追加すればいいのかな。真横からとる、とかは、、ああ、二人の間に綺麗にオブジェクトおけばいいんだ。でかいものをおけば、それだけでいい気がするぞ、、
			
			
			
			if (inputKey != KeyEnum.None) {
				switch (inputKey) {
					case KeyEnum.Send: {
						Debug.LogError("会話対象に送信する。");
						var targetPlayerId = context.talkingPlayerId;
						StackPublish(new Commands.SendMessage(this.playerId, targetPlayerId, context.messageSend));
						context.messageSend = string.Empty;
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
				LeftTalking();
				
				// 会話キーの消費
				if (!string.IsNullOrEmpty(context.messageSend)) {
					context.messageSend = string.Empty;
				}
				
				context.forward = inputDirection;
				context.auto = context.auto.ChangeTo(new Walk<PlayerContext, List<PlayerContext>>(clientFrame, context));
			}
			
			if (context.auto.Contains(AutoConditions.Control.Contorllable)) {
				context.forward = inputDirection;
				context.auto = context.auto.ChangeTo(new Walk<PlayerContext, List<PlayerContext>>(clientFrame, context));
			}
			
			// consume.
			inputDirection = DirectionEnum.None;
		}
	}
	
	GameObject windowInstance;
	
	public void StartTalking (params string[] playerIds) {
		// // ２者間の中間位置にオブジェクトをおく。
		// var centeringObject =
		
		//  会話ウィンドウを出す。
		var windowPrefab = Resources.Load("TalkWindow") as GameObject;
		windowInstance = Instantiate(windowPrefab);
		
		windowInstance.transform.SetParent(uiScreen.transform, false); 
	}
	
	public void LeftTalking () {
		Destroy(windowInstance);
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
	
	public void OnApplicationQuit () {
		WebSocketConnectionController.CloseCurrentConnection();
	}
}
