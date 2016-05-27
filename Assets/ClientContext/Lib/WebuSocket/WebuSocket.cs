using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
		private static RNGCryptoServiceProvider randomGen = new RNGCryptoServiceProvider();
		
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
		
		private int baseBufferSize;
		
		private readonly Action OnConnected;
		private readonly Action OnPinged;
		private readonly Action<Queue<byte[]>> OnMessage;
		private readonly Action<string> OnClosed;
		private readonly Action<string, Exception> OnError;
		
		public WebuSocket (
			string url,
			int baseBufferSize,
			Action OnConnected=null,
			Action<Queue<byte[]>> OnMessage=null,
			Action OnPinged=null,
			Action<string> OnClosed=null,
			Action<string, Exception> OnError=null,
			Dictionary<string, string> additionalHeaderParams=null
		) {
			this.webSocketConnectionId = Guid.NewGuid().ToString();
			this.baseBufferSize = baseBufferSize;
			
			var requstBytesAndHostAndPort = WebuSocketClient.GenerateRequestData(url, additionalHeaderParams);
			this.endPoint = new IPEndPoint(IPAddress.Parse(requstBytesAndHostAndPort.host), requstBytesAndHostAndPort.port);
			
			this.OnConnected = OnConnected;
			this.OnMessage = OnMessage;
			this.OnPinged = OnPinged;
			this.OnClosed = OnClosed;
			this.OnError = OnError;
			
			
			StartConnectAsync(requstBytesAndHostAndPort.requestDataBytes);
		}
		
		private void StartConnectAsync (byte[] requestData) {
			var timeout = 1000;
			
			
			Debug.LogError("起動時に、asyncでtcp -> ヘッダ送る -> レスポンス得る、というのをやってしまう。完了後はargs系を塗り替えるはず。");
			
			socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			socket.NoDelay = true;
			socket.SendTimeout = timeout;
			
			// receiveのargsと、sendのargsを用意する。receiveを立てて云々。全部書ききることができるかな〜〜
			
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
						
						Debug.LogError("接続できなかった。");
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
							Debug.LogError("すでに切断している。");
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
					if (result.Any()) {
						// ここで呼ばれるのはbyteかstringのみ、っていう感じでやりたい。んーーーバラバラに呼ばれるのは避けたいが、ハンドラを渡すわけにも、、
						// やっぱバラバラにしてしまうか？ここでハンドラ着火するか。
						// どうやるのがベストか、、ArraySegmentのみで構成したほうが、取り出しは一発になる。受け取った側で直列化？いやーしんどいな。
						// あ、でもそれでも良いのか。ここで束ねてしまったほうがいいのか。
						// result自体をそれぞれindex持ったarrayにしちゃう？
					}
					
					// check last index for fragmented data.
					var lastData = result.Last();
					var lastDataIndex = (int)(lastData.Offset + lastData.Count);
					
					// if the last result index is matched to whole length, receive finished.
					if (lastDataIndex == token.readableDataLength) {
						ReadyReceivingNewData(token);
						return;
					}
					
					// rest data exists.
					
					var lastDataLength = token.receiveBuffer.Length - lastDataIndex;
					
					// should read rest.
					ReceivingRestData(token, lastDataIndex, lastDataLength);
					return;
				}
				default: {
					throw new Exception("fatal error, could not detect error, receive condition is strange, token.socketState:" + token.socketState);
				}
			}
		}
		
		private void ReadyReceivingNewData (SocketToken token) {
			Debug.LogError("新規データの受付開始");
			token.readableDataLength = 0;
			token.receiveArgs.SetBuffer(token.receiveBuffer, 0, token.receiveBuffer.Length);
			if (!token.socket.ReceiveAsync(token.receiveArgs)) OnReceived(token.socket, token.receiveArgs);
		}
		
		private void ReceivingRestDataWithoutSort (SocketToken token) {
			// should read rest.
			var nextAdditionalBytesLength = token.socket.Available;
		
			if (0 < nextAdditionalBytesLength && token.readableDataLength == token.receiveBuffer.Length) {
				Debug.Log("次のデータが来るのが確定していて、かつバッファサイズが足りない。");
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
				Debug.Log("次のデータが来るのが確定していて、かつバッファサイズが足りない。2");
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
			
			// Disquuun.Log("overflow detected.");
			return -1;
		}
		
		
		private struct SwitchingProtocolData {
			// HTTP/1.1 101 Switching Protocols
			// Server: nginx/1.7.10
			// Date: Sun, 22 May 2016 18:31:47 GMT
			// Connection: upgrade
			// Upgrade: websocket
			// Sec-WebSocket-Accept: C3HoL/ER1LOnEj8yVINdXluouHw=
			
			public SwitchingProtocolData (string source) {
				Debug.LogError("なんか分解して確認しないとな〜みたいな感じがする。");
			}
		}
		
		
		
		public List<ArraySegment<byte>> ScanBuffer (byte[] buffer, long bufferLength) {
			var receivedDataSegments = new List<ArraySegment<byte>>();
			
			int messageHead;
			int cursor = 0;
			
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
						receivedDataSegments.Add(new ArraySegment<byte>(buffer, cursor, length));
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
						Debug.LogError("pong,,! length:" + length);
						PongReceived();
						あーーpayload帰らないから次が取得できないのか〜うーーん、、
						break;
					}
					default: {
						break;
					}
				}
				
				cursor = cursor + length; 
			}
			
			// finally return payload data indexies.
			return receivedDataSegments;
		}
		
		private Action OnPonged;
		public void Ping (Action OnPonged) {
			this.OnPonged = OnPonged;
			var pingBytes = WebSocketByteGenerator.Ping();
			
			socketToken.sendArgs.SetBuffer(pingBytes, 0, pingBytes.Length);
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
			Debug.LogError("pingきたのでpong返さねば");
			var data = WebSocketByteGenerator.Pong();
			if (OnPinged != null) OnPinged();
			
		}
		
		private void PongReceived () {
			if (OnPonged != null) OnPonged();
		}
	}
}