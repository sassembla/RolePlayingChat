using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WebSocketControl;
using XrossPeerUtility;

public class OnExecute : MonoBehaviour {
	private string playerId;// 100 ~ 199の間でランダムにしよう。
	
	public List<PlayerContext> players;
	
	// Use this for initialization
	void Start () {
		players = new List<PlayerContext>();
		
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
			for (var i = 0; i < datas.commands.Length; i++) {
				var containedCommand = datas.commands[i];
				var containedByteData = datas.datas[i].data;
				ExecuteCommandFromBytes(commandSourcePlayerId, containedCommand, containedByteData);
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
				Debug.LogError("EntriedIdをサーバから受け取った");
				var entriedData = Commands.FromData<Commands.EntriedId>(data);
				
				// 新規参加ユーザーを生成
				{
					var entriedPlayerId = entriedData.playerId;
					var playerContext = new PlayerContext(entriedPlayerId);
					var auto = new Spawning<PlayerContext, List<PlayerContext>>(clientFrame, playerContext);
					
					playerContext.auto = auto;
					players.Add(playerContext);
				}
				
				if (commandSourcePlayerId == this.playerId) {
					Debug.LogError("自分がエントリーしたのでSpawnRequestを送る");
					StackPublish(new Commands.SpawnRequest(playerId));
				} else {
					Debug.Log("だれかのエントリーがあった playerId:" + commandSourcePlayerId + " この他人のAutoを生成。");
				}
				return;
			}
			
			case Commands.CommandEnum.Spawn: {
				// 誰かspawnしたんで、Autoの状態を変更する。
				var spawnData = Commands.FromData<Commands.Spawn>(data);
				var spawnPlayerId = spawnData.playerId;
				
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
	
	private void StackPublish (Commands.BaseData data, string[] connectionIds=null) {
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
			Playerたちを動かす。
		*/
		foreach (var playerContext in players) {
			UpdatePlayerContext(playerContext);
		}
		
		PublishStackedData();
		
		clientFrame++;
		if (10000 < clientFrame) clientFrame = 0;
	}
	
	private void UpdatePlayerContext (PlayerContext context) {
		if (context.auto.ShouldFalldown(clientFrame)) {
			// このプレイヤーのこのAuto終わってるので、デフォルトに戻す。
			context.auto = context.auto.ChangeTo(new Default<PlayerContext, List<PlayerContext>>(clientFrame, context));
		}
		
		/*
			自分のキャラの場合、操作を受け付ける。
		*/
		if (context.playerId == this.playerId) {
			ExecuteMyPlayer(context); 
		}
		
		context.auto.Update(clientFrame, players);
	}
	
	private void ExecuteMyPlayer (PlayerContext context) {
		// 上下左右、、、
		if (context.auto.Contains(AutoConditions.Control.Contorllable)) {
			// 受け付けた操作の内容によって、次の動作を変更する。まずは歩こう。
			// キーの入力を斜めに分解して歩く。1ブロック。最後にカメラで斜めにすればイイんで、walkを用意して変えてみよう。
			// 入力に対して、方向をとって、セットする。実際には「向く->歩く->止まる」があるはず。
			context.auto = context.auto.ChangeTo(new Walk<PlayerContext, List<PlayerContext>>(clientFrame, context));
		}
	}
	
	public void OnApplicationQuit () {
		WebSocketConnectionController.CloseCurrentConnection();
	}
}
