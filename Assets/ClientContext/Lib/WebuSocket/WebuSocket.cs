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
			
			
			
			// public Func<DisqueCommand, DisquuunCore.DisquuunResult[], bool> AsyncCallback;
			
			public SocketToken (Socket socket, long bufferSize, SocketAsyncEventArgs connectArgs, SocketAsyncEventArgs sendArgs, SocketAsyncEventArgs receiveArgs) {
				this.socket = socket;
				
				this.receiveBuffer = new byte[bufferSize];
				
				this.connectArgs = connectArgs;
				this.sendArgs = sendArgs;
				this.receiveArgs = receiveArgs;
				
				this.connectArgs.UserToken = this;
				this.sendArgs.UserToken = this;
				this.receiveArgs.UserToken = this;
				
				this.receiveArgs.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
			}
		}
		
		
		public WebuSocket (
			string url,
			Action OnConnected,
			Action<Queue<byte[]>> OnMessage,
			Action<string> OnClosed,
			Action<string, Exception> OnError,
			Dictionary<string, string> additionalHeaderParams=null
		) {
			this.webSocketConnectionId = Guid.NewGuid().ToString();
			
			var requstBytesAndHostAndPort = WebuSocketClient.GenerateRequestData(url, additionalHeaderParams);
			this.endPoint = new IPEndPoint(IPAddress.Parse(requstBytesAndHostAndPort.host), requstBytesAndHostAndPort.port);
			
			StartConnectAsync(requstBytesAndHostAndPort.requestDataBytes);
		}
		
		private void StartConnectAsync (byte[] requestData) {
			var timeout = 1000;
			var bufferSize = 1024 * 10;
			
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
			sendArgs.SetBuffer(requestData, 0, requestData.Length);
			sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSend);
			
			var receiveArgs = new SocketAsyncEventArgs();
			receiveArgs.AcceptSocket = clientSocket;
			receiveArgs.RemoteEndPoint = endPoint;
			receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnReceived);
						
			socketToken = new SocketToken(clientSocket, bufferSize, connectArgs, sendArgs, receiveArgs); 
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
					
					// send.
					if (!token.socket.SendAsync(socketToken.sendArgs)) OnSend(token.socket, token.sendArgs);
					
					return;
				}
				default: {
					throw new Exception("socket state does not correct:" + token.socketState);
				}
			}
		}
		
		private void OnClosed (object unused, SocketAsyncEventArgs args) {
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
					Debug.LogError("送信成功してる");
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
			
			if (0 < args.BytesTransferred) {
				var bytesAmount = args.BytesTransferred;
				
				// 総合的な長さ。このサイズを超えて読むことはできない。
				
				// update as read completed.
				token.readableDataLength = token.readableDataLength + bytesAmount;
				
				switch (token.socketState) {
					case SocketState.OPENING: {
						Debug.LogError("openingデータがある");	
						var lineEndCursor = ReadUpgradeLine(args.Buffer, 0, token.readableDataLength);
						if (lineEndCursor == -1) {
							// まだ読み終われてないので、えーーーっと、、SetBufferの必要がある。
							break;
						}
						
						var protocolData = new SwitchingProtocolData(Encoding.UTF8.GetString(args.Buffer, 0, lineEndCursor));
						token.socketState = SocketState.OPENED;
						break;
					}
					case SocketState.OPENED: {
						var result = ScanBuffer(token.receiveBuffer, token.readableDataLength);
						if (result.Any()) {
							// 読み終わっている場所まではなんとかする。で、末尾のデータのindex + lengthがtoken.readableDataLengthとマッチしない場合、
							// 
						}
						break;
					}
				}
				
				// 
				
				if (true) {
				// 	var continuation = token.AsyncCallback(token.currentCommand, result.data);
				// 	if (continuation) {
				// 		// ready for loop receive.
				// 		token.readableDataLength = 0;
				// 		token.receiveArgs.SetBuffer(token.receiveBuffer, 0, token.receiveBuffer.Length);
				// 		if (!token.socket.ReceiveAsync(token.receiveArgs)) OnReceived(token.socket, token.receiveArgs);
						
				// 		token.sendArgs.SetBuffer(token.currentSendingBytes, 0, token.currentSendingBytes.Length);
				// 		if (!token.socket.SendAsync(token.sendArgs)) OnSend(token.socket, token.sendArgs);
				// 	} else {
				// 		switch (token.socketState) {
				// 			case SocketState.BUSY: {
				// 				token.socketState = SocketState.OPENED;
				// 				break;
				// 			}
				// 			case SocketState.DISPOSABLE_BUSY: {
				// 				// disposable connection should be close after used.
				// 				StartCloseAsync();
				// 				break;
				// 			}
				// 		}
				// 	}
					ReadyReceive(token);
				} else {
					var nextAdditionalBytesLength = token.socket.Available;
					
					if (token.readableDataLength == token.receiveBuffer.Length) {
						Debug.Log("次のデータが来るのが確定していて、かつバッファサイズが足りない。");
						Array.Resize(ref token.receiveBuffer, token.receiveArgs.Buffer.Length + nextAdditionalBytesLength);
					}
					
					var receivableCount = token.receiveBuffer.Length - token.readableDataLength;
					token.receiveArgs.SetBuffer(token.receiveBuffer, token.readableDataLength, receivableCount);
					if (!token.socket.ReceiveAsync(token.receiveArgs)) OnReceived(token.socket, token.receiveArgs);
				}
			} else {
				Debug.LogError("データがない");
				// ReadyReceive(token);
			}
		}
		
		private void ReadyReceive (SocketToken token) {
			Debug.LogError("新規データの受付開始");
			token.readableDataLength = 0;
			token.receiveArgs.SetBuffer(token.receiveBuffer, 0, token.receiveBuffer.Length);
			if (!token.socket.ReceiveAsync(token.receiveArgs)) OnReceived(token.socket, token.receiveArgs);
		}
		
		public void Close () {
			Debug.LogError("close!");
			try {
				socketToken.socket.Close();
			} catch (Exception e) {
				Debug.LogError("e:" + e);
			}
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
				
			}
		}
		
		private const byte OPFilter			= 0xF;// 1111
		
		public static List<OpCodeAndPayloadIndex> ScanBuffer (byte[] buffer, long bufferLength) {
			var opCodeAndPayloadIndexies = new List<OpCodeAndPayloadIndex>();
			
			uint messageHead;
			uint cursor = 0;
			
			while (cursor < bufferLength) {
				messageHead = cursor;
				
				// first byte = fin(1), rsv1(1), rsv2(1), rsv3(1), opCode(4)
				var opCode = (byte)(buffer[cursor++] & OPFilter);
				
				// second byte = mask(1), length(7)
				if (bufferLength < cursor) break; 
				/*
					mask of data from server is definitely zero(0).
					ignore reading mask bit.
				*/
				uint length = (uint)buffer[cursor++];
				switch (length) {
					case 126: {
						// next 2 byte is length data.
						if (bufferLength < cursor + 2) break;
						
						length = (uint)(
							(buffer[cursor++] << 8) +
							(buffer[cursor++])
						);
						break;
					}
					case 127: {
						// next 8 byte is length data.
						if (bufferLength < cursor + 8) break;
						
						length = (uint)(
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
				opCodeAndPayloadIndexies.Add(new OpCodeAndPayloadIndex(opCode, cursor, length));
				
				cursor = cursor + length; 
			}
			
			// finally return indexies.
			return opCodeAndPayloadIndexies;
		}
		
		public struct OpCodeAndPayloadIndex {
			public readonly byte opCode;
			public readonly uint start;
			public readonly uint length;
			public OpCodeAndPayloadIndex (byte opCode, uint start, uint length) {
				this.opCode = opCode;
				this.start = start;
				this.length = length;
			}
		}
	}
}