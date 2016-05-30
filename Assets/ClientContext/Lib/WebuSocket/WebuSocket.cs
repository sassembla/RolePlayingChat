using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace WebuSocketCore {
    public enum SocketState {
		CONNECTING,
		OPENING,
		OPENED,
		CLOSING,
		CLOSED
	}
	
	public class WebuSocket {
		private readonly EndPoint endPoint;
		
		private SocketToken socketToken;
		
		public string webSocketConnectionId;
		
		private Socket socket;
		
		public class SocketToken {
			public SocketState socketState;
			public readonly Socket socket;
			
			public byte[] receiveBuffer;
			public int readableDataLength;
			
			public readonly SocketAsyncEventArgs connectArgs;
			public readonly SocketAsyncEventArgs sendArgs;
			public readonly SocketAsyncEventArgs receiveArgs;
			
			public SocketToken (Socket socket, int bufferLen, SocketAsyncEventArgs connectArgs, SocketAsyncEventArgs sendArgs, SocketAsyncEventArgs receiveArgs) {
				this.socket = socket;
				
				this.receiveBuffer = new byte[bufferLen];
				
				this.connectArgs = connectArgs;
				this.sendArgs = sendArgs;
				this.receiveArgs = receiveArgs;
				
				this.connectArgs.UserToken = this;
				this.sendArgs.UserToken = this;
				this.receiveArgs.UserToken = this;
				
				this.receiveArgs.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
			}
		}
		
		private readonly int baseBufferSize;
		
		private readonly Action OnConnected;
		private readonly Action OnPinged;
		private readonly Action<Queue<ArraySegment<byte>>> OnMessage;
		private readonly Action<string> OnClosed;
		private readonly Action<string, Exception> OnError;
		
		private readonly string base64Key;
		
		public WebuSocket (
			string url,
			int baseBufferSize,
			Action OnConnected=null,
			Action<Queue<ArraySegment<byte>>> OnMessage=null,
			Action OnPinged=null,
			Action<string> OnClosed=null,
			Action<string, Exception> OnError=null,
			Dictionary<string, string> additionalHeaderParams=null
		) {
			this.webSocketConnectionId = Guid.NewGuid().ToString();
			this.baseBufferSize = baseBufferSize;
			
			this.base64Key = WebSocketByteGenerator.GeneratePrivateBase64Key();
			
			var requstBytesAndHostAndPort = GenerateRequestData(url, additionalHeaderParams, base64Key);
			this.endPoint = new IPEndPoint(IPAddress.Parse(requstBytesAndHostAndPort.host), requstBytesAndHostAndPort.port);
			
			this.OnConnected = OnConnected;
			this.OnMessage = OnMessage;
			this.OnPinged = OnPinged;
			this.OnClosed = OnClosed;
			this.OnError = OnError;
			
			StartConnectAsync(requstBytesAndHostAndPort.requestDataBytes);
		}
		
		
		private const string CRLF = "\r\n";
		private const string WEBSOCKET_VERSION = "13"; 
		
		private static RequestDataBytesAndHostAndPort GenerateRequestData (string urlSource, Dictionary<string, string> additionalHeaderParams, string base64Key) {
			var uri = new Uri(urlSource);
			
			var method = "GET";
			var host = uri.Host;
			var schm = uri.Scheme;
			var port = uri.Port;
			
			Debug.LogError("wss setting.");
			var requestHeaderParams = new Dictionary<string, string>{
				{"Host", (port == 80 && schm == "ws") || (port == 443 && schm == "wss") ? uri.DnsSafeHost : uri.Authority},
				{"Upgrade", "websocket"},
				{"Connection", "Upgrade"},
				{"Sec-WebSocket-Key", base64Key},
				{"Sec-WebSocket-Version", WEBSOCKET_VERSION}
			};
			
			if (additionalHeaderParams != null) { 
				foreach (var key in additionalHeaderParams.Keys) requestHeaderParams[key] = additionalHeaderParams[key];
			}
			
			/*
				construct request bytes data.
			*/
			var requestData = new StringBuilder();
			
			requestData.AppendFormat("{0} {1} HTTP/{2}{3}", method, uri, "1.1", CRLF);

			foreach (var key in requestHeaderParams.Keys) requestData.AppendFormat("{0}: {1}{2}", key, requestHeaderParams[key], CRLF);

			requestData.Append(CRLF);

			var entity = string.Empty;
			requestData.Append(entity);
			
			return new RequestDataBytesAndHostAndPort(host, port, Encoding.UTF8.GetBytes(requestData.ToString().ToCharArray()));
		}
		
		
		public struct RequestDataBytesAndHostAndPort {
			public string host;
			public int port;
			public byte[] requestDataBytes;
			
			public RequestDataBytesAndHostAndPort (string host, int port, byte[] requestDataBytes) {
				this.host = host;
				this.port = port;
				this.requestDataBytes = requestDataBytes;
			}
		}
		
		
		private void StartConnectAsync (byte[] requestData) {
			Debug.LogError("timeout setting.");
			var timeout = 1000;
			
			socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			socket.NoDelay = true;
			socket.SendTimeout = timeout;
			
			var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			clientSocket.NoDelay = true;
			
			var connectArgs = new SocketAsyncEventArgs();
			connectArgs.AcceptSocket = clientSocket;
			connectArgs.RemoteEndPoint = endPoint;
			connectArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnConnect);
			
			var sendArgs = new SocketAsyncEventArgs();
			sendArgs.AcceptSocket = clientSocket;
			sendArgs.RemoteEndPoint = endPoint;
			sendArgs.SetBuffer(requestData, 0, requestData.Length);// set websocket handshake data.
			sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSend);
			
			var receiveArgs = new SocketAsyncEventArgs();
			receiveArgs.AcceptSocket = clientSocket;
			receiveArgs.RemoteEndPoint = endPoint;
			receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnReceived);
						
			socketToken = new SocketToken(clientSocket, baseBufferSize, connectArgs, sendArgs, receiveArgs); 
			socketToken.socketState = SocketState.CONNECTING;
			
			// start connect.
			if (!clientSocket.ConnectAsync(socketToken.connectArgs)) OnConnect(clientSocket, connectArgs);
		}
		
		private void OnConnect (object unused, SocketAsyncEventArgs args) {
			var token = (SocketToken)args.UserToken;
			switch (token.socketState) {
				case SocketState.CONNECTING: {
					if (args.SocketError != SocketError.Success) {
						token.socketState = SocketState.CLOSED;
						var error = new Exception("connect error:" + args.SocketError.ToString());
						
						Debug.LogError("接続できなかったエラー1");
						// SocketClosed(this, error);
						return;
					}
					
					token.socketState = SocketState.OPENING;
					
					// ready receive.
					socketToken.readableDataLength = 0;
					socketToken.receiveArgs.SetBuffer(socketToken.receiveBuffer, 0, socketToken.receiveBuffer.Length);
					if (!socketToken.socket.ReceiveAsync(socketToken.receiveArgs)) OnReceived(socketToken.socket, socketToken.receiveArgs);
					
					// send. websocket handshake request data is already set.
					if (!token.socket.SendAsync(socketToken.sendArgs)) OnSend(token.socket, token.sendArgs);
					return;
				}
				default: {
					throw new Exception("socket state does not correct:" + token.socketState);
				}
			}
		}
		
		private void OnDisconnected (object unused, SocketAsyncEventArgs args) {
			var token = (SocketToken)args.UserToken;
			switch (token.socketState) {
				case SocketState.CLOSED: {
					// do nothing.
					break;
				}
				default: {
					token.socketState = SocketState.CLOSED;
					break;
				}
			}
		}
		
		private void OnSend (object unused, SocketAsyncEventArgs args) {
			var socketError = args.SocketError;
			switch (socketError) {
				case SocketError.Success: {
					// do nothing.
					// Debug.LogError("送信成功してる");
					break;
				}
				default: {
					Debug.LogError("まだエラーハンドルしてない。切断の一種なんだけど、非同期実行してるAPIに紐付けることができる。");
					// if (Error != null) {
					// 	var error = new Exception("send error:" + socketError.ToString());
					// 	Error(error);
					// }
					break;
				}
			}
		}
		
		private void OnReceived (object unused, SocketAsyncEventArgs args) {
			var token = (SocketToken)args.UserToken;
			
			if (args.SocketError != SocketError.Success) { 
				switch (token.socketState) {
					case SocketState.CLOSING:
					case SocketState.CLOSED: {
						// already closing, ignore.
						return;
					}
					default: {
						// show error, then close or continue receiving.
						Debug.LogError("まだエラーハンドルしてない2。切断の一種なんだけど、非同期実行してるAPIに紐付けることができる、、、かなあ？　できない気もしてきたぞ。");
						// if (Error != null) {
						// 	var error = new Exception("receive error:" + args.SocketError.ToString() + " size:" + args.BytesTransferred);
						// 	Error(error);
						// }
						
						// connection is already closed.
						if (!IsSocketConnected(token.socket)) {
							Debug.LogError("すでに切断している状態での受け取り。");
							// Disconnect();
							return;
						}
						
						// continue receiving data. go to below.
						break;
					}
				}
			}
			
			if (args.BytesTransferred == 0) throw new Exception("failed to receive. args.BytesTransferred = 0.");
			
			// update as read completed.
			token.readableDataLength = token.readableDataLength + args.BytesTransferred;
			
			switch (token.socketState) {
				case SocketState.OPENING: {
					var lineEndCursor = ReadUpgradeLine(args.Buffer, 0, token.readableDataLength);
					if (lineEndCursor != -1) {
						var protocolData = new SwitchingProtocolData(Encoding.UTF8.GetString(args.Buffer, 0, lineEndCursor));
						var expectedKey = WebSocketByteGenerator.GenerateExpectedAcceptedKey(base64Key);
						
						Debug.LogError("接続時のバリデーション失敗=偽装サーバとかの可能性");
						if (protocolData.securityAccept != expectedKey) {
							Debug.LogError("key not match. protocolData.securityAccept:" + protocolData.securityAccept + " expectedKey:" + expectedKey);
							throw new Exception("fatal error.");
						}  
						token.socketState = SocketState.OPENED;
						
						ReadyReceivingNewData(token);
						
						if (OnConnected != null) OnConnected();
						return;
					}
					
					// should read next.
					ReceivingRestDataWithoutSort(token);
					return;
				}
				case SocketState.OPENED: {
					var result = ScanBuffer(token.receiveBuffer, token.readableDataLength);
					
					// read completed datas.
					if (result.segments.Any()) {
						OnMessage(result.segments);
					}
					
					// if the last result index is matched to whole length, receive finished.
					if (result.lastDataTail == token.readableDataLength) {
						ReadyReceivingNewData(token);
						return;
					}
					
					// rest data exists.
					
					var alreadyReceivedDataLength = token.receiveBuffer.Length - result.lastDataTail;
					
					// should read rest.
					ReceivingRestData(token, result.lastDataTail, alreadyReceivedDataLength);
					return;
				}
				default: {
					throw new Exception("fatal error, could not detect error, receive condition is strange, token.socketState:" + token.socketState);
				}
			}
		}
		
		private void ReadyReceivingNewData (SocketToken token) {
			token.readableDataLength = 0;
			token.receiveArgs.SetBuffer(token.receiveBuffer, 0, token.receiveBuffer.Length);
			if (!token.socket.ReceiveAsync(token.receiveArgs)) OnReceived(token.socket, token.receiveArgs);
		}
		
		private void ReceivingRestDataWithoutSort (SocketToken token) {
			// should read rest.
			var nextAdditionalBytesLength = token.socket.Available;
		
			if (0 < nextAdditionalBytesLength && token.readableDataLength == token.receiveBuffer.Length) {
				Debug.LogError("次のデータが来るのが確定していて、かつバッファサイズが足りない。リサイズが発生している。");
				Array.Resize(ref token.receiveBuffer, token.receiveArgs.Buffer.Length + nextAdditionalBytesLength);
			}
			
			var receivableCount = token.receiveBuffer.Length - token.readableDataLength;
			token.receiveArgs.SetBuffer(token.receiveBuffer, token.readableDataLength, receivableCount);
			if (!token.socket.ReceiveAsync(token.receiveArgs)) OnReceived(token.socket, token.receiveArgs);
		}
		
		private void ReceivingRestData (SocketToken token, int restDataIndex, int alreadyReadLength) {
			// move already received data index to head.
			Array.Copy(token.receiveBuffer, restDataIndex, token.receiveBuffer, 0, alreadyReadLength);
			
			token.readableDataLength = alreadyReadLength;
			
			// should read rest.
			var nextAdditionalBytesLength = token.socket.Available;
			
			/*
				if next data is too large to read at once, scale up buffer.
				this causes token.receiveBuffer's pointer change & length change.
			*/
			if (0 < nextAdditionalBytesLength && token.receiveBuffer.Length - token.readableDataLength < nextAdditionalBytesLength) {
				Debug.LogError("次のデータが来るのが確定していて、かつバッファサイズが足りない。リサイズが発生している。2");
				Array.Resize(ref token.receiveBuffer, token.receiveArgs.Buffer.Length + nextAdditionalBytesLength);
			}
			
			// set buffer size and index with latest length of token.receiveBuffer.
			token.receiveArgs.SetBuffer(token.receiveBuffer, alreadyReadLength, token.receiveBuffer.Length - token.readableDataLength);
			if (!token.socket.ReceiveAsync(token.receiveArgs)) OnReceived(token.socket, token.receiveArgs);
		}
		
		public void Disconnect (bool force=false) {
			if (force) {
				try {
					socketToken.socket.Close();
				} catch (Exception e) {
					Debug.LogError("e:" + e);
				}
				return;
			}
			
			switch (socketToken.socketState) {
				case SocketState.CLOSING:
				case SocketState.CLOSED: {
					// do nothing
					break;
				}
				default: {
					socketToken.socketState = SocketState.CLOSING;
					
					StartCloseAsync();
					break;
				}
			}
		}
		
		private void StartCloseAsync () {
			var closeEventArgs = new SocketAsyncEventArgs();
			closeEventArgs.UserToken = socketToken;
			closeEventArgs.AcceptSocket = socketToken.socket;
			closeEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnDisconnected);
			
			if (!socketToken.socket.DisconnectAsync(closeEventArgs)) OnDisconnected(socketToken.socket, closeEventArgs);
		}
		
		private static bool IsSocketConnected (Socket s) {
			bool part1 = s.Poll(1000, SelectMode.SelectRead);
			bool part2 = (s.Available == 0);
			
			if (part1 && part2) return false;
			
			return true;
		}
		
		public static byte ByteCR = Convert.ToByte('\r');
		public static byte ByteLF = Convert.ToByte('\n');
		public static int ReadUpgradeLine (byte[] bytes, int cursor, long length) {
			while (cursor < length) {
				if (4 < cursor && 
					bytes[cursor - 3] == ByteCR && 
					bytes[cursor - 2] == ByteLF &&
					bytes[cursor - 1] == ByteCR && 
					bytes[cursor] == ByteLF
				) return cursor - 1;
				
				cursor++;
			}
			
			return -1;
		}
		
		
		private class SwitchingProtocolData {
			// HTTP/1.1 101 Switching Protocols
			// Server: nginx/1.7.10
			// Date: Sun, 22 May 2016 18:31:47 GMT
			// Connection: upgrade
			// Upgrade: websocket
			// Sec-WebSocket-Accept: C3HoL/ER1LOnEj8yVINdXluouHw=
			
			public string protocolDesc;
			public string httpResponseCode;
			public string httpMessage;
			public string serverInfo;
			public string date;
			public string connectionType;
			public string upgradeMethod;
			public string securityAccept;
			
			public SwitchingProtocolData (string source) {
				var acceptedResponseHeaderKeyValues = source.Split('\n');
				foreach (var line in acceptedResponseHeaderKeyValues) {
					if (line.StartsWith("HTTP")) {
						var httpResponseHeaderSplitted = line.Split(' ');
						this.protocolDesc = httpResponseHeaderSplitted[0];
						this.httpResponseCode = httpResponseHeaderSplitted[1];
						this.httpMessage = httpResponseHeaderSplitted[2] + httpResponseHeaderSplitted[3];
						continue;
					}
					
					if (!line.Contains(": ")) continue;
					
					var keyAndValue = line.Replace(": ", ":").Split(':');
					
					switch (keyAndValue[0]) {
						case "Server": {
							this.serverInfo = keyAndValue[1];
							break;
						}
						case "Date": {
							this.date = keyAndValue[1];
							break;
						}
						case "Connection": {
							this.connectionType = keyAndValue[1];
							break;
						}
						case "Upgrade": {
							this.upgradeMethod = keyAndValue[1];
							break;
						}
						case "Sec-WebSocket-Accept": {
							this.securityAccept = keyAndValue[1].TrimEnd();
							break;
						}
						default: {
							Debug.LogError("invalid key value found. line:" + line);
							throw new Exception("invalid key value found. line:" + line);
						}
					}
				}
			}
		}
		
		private WebuSocketResults ScanBuffer (byte[] buffer, long bufferLength) {
			Queue<ArraySegment<byte>> receivedDataSegments = new Queue<ArraySegment<byte>>();
			
			int messageHead = 0;
			int cursor = 0;
			int lastDataEnd = 0;
			while (cursor < bufferLength) {
				messageHead = cursor;
				
				// first byte = fin(1), rsv1(1), rsv2(1), rsv3(1), opCode(4)
				var opCode = (byte)(buffer[cursor++] & WebSocketByteGenerator.OPFilter);
				
				// second byte = mask(1), length(7)
				if (bufferLength < cursor) break;
				
				/*
					mask of data from server is definitely zero(0).
					ignore reading mask bit.
				*/
				int length = buffer[cursor++];
				switch (length) {
					case 126: {
						// next 2 byte is length data.
						if (bufferLength < cursor + 2) break;
						
						length = (
							(buffer[cursor++] << 8) +
							(buffer[cursor++])
						);
						break;
					}
					case 127: {
						// next 8 byte is length data.
						if (bufferLength < cursor + 8) break;
						
						length = (
							(buffer[cursor++] << (8*7)) +
							(buffer[cursor++] << (8*6)) +
							(buffer[cursor++] << (8*5)) +
							(buffer[cursor++] << (8*4)) +
							(buffer[cursor++] << (8*3)) +
							(buffer[cursor++] << (8*2)) +
							(buffer[cursor++] << 8) +
							(buffer[cursor++])
						);
						break;
					}
					default: {
						// other.
						break;
					}
				}
				
				// read payload data.
				if (bufferLength < cursor + length) break;
				
				// payload is fully contained!
				switch (opCode) {
					case WebSocketByteGenerator.OP_CONTINUATION: {
						throw new Exception("unsupported.");
					}
					case WebSocketByteGenerator.OP_TEXT: {
						throw new Exception("unsupported.");						
					}
					case WebSocketByteGenerator.OP_BINARY: {
						receivedDataSegments.Enqueue(new ArraySegment<byte>(buffer, cursor, length));
						break;
					}
					case WebSocketByteGenerator.OP_CLOSE: {
						CloseReceived();
						break;
					}
					case WebSocketByteGenerator.OP_PING: {
						PingReceived();
						break;
					}
					case WebSocketByteGenerator.OP_PONG: {
						PongReceived();
						break;
					}
					default: {
						break;
					}
				}
				
				cursor = cursor + length;
				
				// set end of data.
				lastDataEnd = cursor;
			}
			
			// finally return payload data indexies.
			return new WebuSocketResults(receivedDataSegments, lastDataEnd);
		}
		
		private struct WebuSocketResults {
			public Queue<ArraySegment<byte>> segments;
			public int lastDataTail;
			
			public WebuSocketResults (Queue<ArraySegment<byte>> segments, int lastDataTail) {
				this.segments = segments;
				this.lastDataTail = lastDataTail;
			}
		}
		
		private Action OnPonged;
		public void Ping (Action OnPonged) {
			this.OnPonged = OnPonged;
			var pingBytes = WebSocketByteGenerator.Ping();
			
			socketToken.sendArgs.SetBuffer(pingBytes, 0, pingBytes.Length);
			if (!socketToken.socket.SendAsync(socketToken.sendArgs)) OnSend(socketToken.socket, socketToken.sendArgs);	
		}
		
		public void Send (byte[] data) {
			var payloadBytes = WebSocketByteGenerator.SendBinaryData(data);
			
			socketToken.sendArgs.SetBuffer(payloadBytes, 0, payloadBytes.Length);
			if (!socketToken.socket.SendAsync(socketToken.sendArgs)) OnSend(socketToken.socket, socketToken.sendArgs);
		}
		
		
		
		private void CloseReceived () {
			switch (socketToken.socketState) {
				case SocketState.OPENED: {
					Debug.LogError("非同期の切断処理に入る。サーバからclose受け取った場合だね。");
					if (OnClosed != null) OnClosed("disconnected from server.");
					Disconnect();
					break;
				}
				default: {
					
					break;
				}
			}
		}
		
		private void PingReceived () {
			if (OnPinged != null) OnPinged();
			
			var pongBytes = WebSocketByteGenerator.Pong();
			socketToken.sendArgs.SetBuffer(pongBytes, 0, pongBytes.Length);
			if (!socketToken.socket.SendAsync(socketToken.sendArgs)) OnSend(socketToken.socket, socketToken.sendArgs);	
		}
		
		private void PongReceived () {
			Debug.LogError("pong受け取った");
			if (OnPonged != null) OnPonged();
		}
	}
}