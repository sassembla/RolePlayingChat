using UnityEngine;
using System.Collections;
using XrossPeerUtility;
using WebSocketControl;
using System.Collections.Generic;

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
		
		XrossPeer.SetupLog(string.Empty, Debug.Log);
		
		// クライアント接続を開始して、接続できたら云々。
		WebSocketConnectionController.InitWebSocketConnection(
			new Dictionary<string, string>{
				{"playerId", "100"}
			},//Dictionary<string, string> customHeaderKeyValues, 
			"rolePlayAgent", // string agent,
			() => {
				XrossPeer.Log("connected!");
			},// Action connected, 
			(List<byte[]> datas) => {
				XrossPeer.Log("data incomming!");
			},// Action<List<byte[]>> onBinaryMessage,
			(connectionFailedReason) => {
				XrossPeer.Log("connection failed, connectionFailedReason:" + connectionFailedReason);
			},// Action<string> connectionFailed, 
			(disconnectedReason) => {
				XrossPeer.Log("disconnected, reason:" + disconnectedReason);
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
