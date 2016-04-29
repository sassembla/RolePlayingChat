using UnityEngine;
using System.Collections;
using WebSocketControl;
using System.Collections.Generic;
using System;

public class OnExecute : MonoBehaviour {
	
	WebSocketConnectionController webSocketCont;
	
	int count = 0;
	
	// Use this for initialization
	IEnumerator Start () {
		while (true) {
			count ++;
			if (count == 100) break; 
			yield return null;
		}
		
		// クライアント接続を開始して、接続できたら云々。
		WebSocketConnectionController.InitWebSocketConnection(
			new Dictionary<string, string>{
				{"playerId", Guid.NewGuid().ToString()}
			},//Dictionary<string, string> customHeaderKeyValues, 
			"rolePlayAgent", // string agent,
			() => {
				Debug.LogError("connected!");
			},// Action connected, 
			(List<byte[]> datas) => {
				Debug.LogError("data incomming!");
			},// Action<List<byte[]>> onBinaryMessage,
			(connectionFailedReason) => {
				Debug.LogError("connection failed, connectionFailedReason:" + connectionFailedReason);
			},// Action<string> connectionFailed, 
			(disconnectedReason) => {
				Debug.LogError("disconnected, reason:" + disconnectedReason);
			},// Action<string> disconnected,
			false,// bool autoReconnect,
			() => {}// Action reconnected
		);
	}
	
	
	// Update is called once per frame
	void Update () {
	
	}
	
	public void OnApplicationQuit () {
		WebSocketConnectionController.CloseCurrentConnection();
	}
}
