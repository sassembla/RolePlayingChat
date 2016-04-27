using System;
using System.Collections.Generic;
using XrossPeerUtility;
/**
	第二レイヤ
	予約を元にConnectionをゲームへと送り込む。
	ただのゲート。
*/
public class ReservationLayer {
	private readonly string reservationLayerId;
	
	private GameContextLayer gameLayer;
	
	public ReservationLayer (Action<object, string> publish) {
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "とりあえず通過できるtokenとして特定のplayerIdを直書きしてある。");
		
		var reservedPlayerIds = new List<string>{
			"100", "_empty_",
		};
		
		reservationLayerId = Guid.NewGuid().ToString();
		gameLayer = new GameContextLayer(reservedPlayerIds, publish);
	}
	
	public void EnqueueOnConnect (string connectionId, string token) {
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "connectionIdとplayerDataが揃った状態でくる。ので、ここで照会を行ってしまおう。token:" + token);
		
		var playerId = token;
		XrossPeer.Log("playerId:" + playerId + " connectionId:" + connectionId);
		
		// set connectionId to reserved playerId.
		var succeeded = gameLayer.SetConnectionIdOfPlayerId(playerId, connectionId);
		if (!succeeded) return;
		
		if (playerId == "_empty_") {
			XrossPeer.Log("空のユーザーなんで、接続認定できたらここで引き返す");
			return;
		}
		
		var data = new Commands.OnConnected(playerId).ToData();
		if (true) gameLayer.EnqueOnReceive(connectionId, data);
	}
	
	public void EnqueueOnMessage (string connectionId, byte[] data) {
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "とりあえずすべてのconnectionIdに対して、このreserveレイヤに登録があった、みたいなみなしをしてうけいれる。実際にはReservationLayerが複数のGameLayerをもっていて、特定の情報を元にGameContextLayerへとメッセージをふり分ける。");
		if (true) gameLayer.EnqueOnReceive(connectionId, data);
	}
	
	public void EnqueueOnDisconnect (string connectionId, string token, string reason) {
		var playerId = token;
		var data = new Commands.OnDisconnected(playerId, reason).ToData();
		
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "disconnect. とりあえずすべてのconnectionIdに対して、このreserveレイヤに登録があった、みたいなみなしをしてうけいれる。");
		if (true) {
			/*
				ここでEnqueueしておくと、接続情報の消去前にenqueueされる。
				で、実際のframeでの実行時には、
			*/
			gameLayer.EnqueOnReceive(connectionId, data);
			
			// remove connectionId from reserved playerId.
			var succeeded = gameLayer.DiscardConnectionIdOfPlayerId(playerId);
			if (!succeeded) XrossPeer.Log("playerId:" + playerId + " のconnectionの廃棄に失敗した。ふむ、、");
			else XrossPeer.Log("playerId:" + playerId + " のConnectionの廃棄に成功した。");
		}
	}
}