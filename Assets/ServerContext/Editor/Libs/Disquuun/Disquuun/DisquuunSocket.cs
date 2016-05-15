using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DisquuunCore {
    public class DisquuunSocket {
		private Action<DisquuunSocket, Exception> ConnectionFailed;
		
		private SocketToken socketToken;
		
		public SocketState State () {
			// lock (socketToken) 
			{
				return socketToken.socketState;
			}
		}
		
		public enum SocketState {
			OPENING,
			OPENED,
			BUSY,
			CLOSING,
			CLOSED
		}
		
		public class SocketToken {
			public SocketState socketState;
			
			public readonly Socket socket;
			
			public byte[] receiveBuffer;
			
			public readonly SocketAsyncEventArgs connectArgs;
			public readonly SocketAsyncEventArgs sendArgs;
			public readonly SocketAsyncEventArgs receiveArgs;
			
			public DisqueCommand currentCommand;
			public byte[] currentBytes;
			
			public Func<DisqueCommand, DisquuunCore.DisquuunResult[], bool> AsyncCallback;
			
			public SocketToken (Socket socket, long bufferSize, SocketAsyncEventArgs connectArgs, SocketAsyncEventArgs sendArgs, SocketAsyncEventArgs receiveArgs) {
				this.socketState = SocketState.OPENING;
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
		
		public DisquuunSocket (IPEndPoint endPoint, long bufferSize, Action<DisquuunSocket, Exception> ConnectionFailed) {
			this.ConnectionFailed = ConnectionFailed;
			
			var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			
			var connectArgs = new SocketAsyncEventArgs();
			connectArgs.AcceptSocket = clientSocket;
			connectArgs.RemoteEndPoint = endPoint;
			connectArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnConnected);
			
			var sendArgs = new SocketAsyncEventArgs();
			sendArgs.AcceptSocket = clientSocket;
			sendArgs.RemoteEndPoint = endPoint;
			sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSend);
			
			var receiveArgs = new SocketAsyncEventArgs();
			receiveArgs.AcceptSocket = clientSocket;
			receiveArgs.RemoteEndPoint = endPoint;
			receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnReceived);
						
			socketToken = new SocketToken(clientSocket, bufferSize, connectArgs, sendArgs, receiveArgs); 
			
			// start connect.
			if (!clientSocket.ConnectAsync(socketToken.connectArgs)) OnConnected(clientSocket, connectArgs);
		}
		
		
		/*
			Core methods of Disquuun.
		*/
		public DisquuunResult[] Sync (DisqueCommand command, byte[] data) {
			socketToken.socketState = SocketState.BUSY;
			socketToken.socket.Send(data);
			
			// TestLogger.Log("send失敗とかもありえるはず。");
			
			var currentLength = 0;
			var scanResult = new DisquuunAPI.ScanResult(false);
			
			while (true) {
				// waiting for head of transferring data or rest of data.
				socketToken.socket.Receive(socketToken.receiveBuffer, currentLength, 1, SocketFlags.None);
				currentLength = currentLength + 1;
				
				var available = socketToken.socket.Available;
				{
					var readableLength = currentLength + available;
					
					if (socketToken.receiveBuffer.Length < readableLength) {
						TestLogger.Log("サイズオーバーしてる " + socketToken.receiveBuffer.Length + " vs:" + readableLength);
						Array.Resize(ref socketToken.receiveBuffer, readableLength);
					} else {
						// TestLogger.Log("まだサイズオーバーしてない " + socketToken.receiveBuffer.Length + " vs:" + readableLength + " が、読み込みの過程でサイズオーバーしそう。");
					}
				}
				
				// read rest.
				socketToken.socket.Receive(socketToken.receiveBuffer, currentLength, available, SocketFlags.None);
				currentLength = currentLength + available;
				
				scanResult = DisquuunAPI.ScanBuffer(command, socketToken.receiveBuffer, currentLength);
				if (scanResult.isDone) break;
				
				// continue reading data from socket.
				TestLogger.Log("continue, currentLength:" + currentLength);
			}
			
			socketToken.socketState = SocketState.OPENED;
			return scanResult.data;
		}
		
		public void Async (DisqueCommand command, byte[] data, Func<DisqueCommand, DisquuunResult[], bool> Callback) {
			socketToken.socketState = SocketState.BUSY;
			
			// ready for receive.
			if (!socketToken.socket.ReceiveAsync(socketToken.receiveArgs)) OnReceived(socketToken.socket, socketToken.receiveArgs);
			
			socketToken.currentCommand = command;
			socketToken.AsyncCallback = Callback;
			socketToken.sendArgs.SetBuffer(data, 0, data.Length);
			if (!socketToken.socket.SendAsync(socketToken.sendArgs)) OnSend(socketToken.socket, socketToken.sendArgs);
		}
		
		public void Loop (DisqueCommand command, byte[] data, Func<DisqueCommand, DisquuunResult[], bool> Callback) {
			socketToken.socketState = SocketState.BUSY;
			
			// ready for receive.
			if (!socketToken.socket.ReceiveAsync(socketToken.receiveArgs)) OnReceived(socketToken.socket, socketToken.receiveArgs);
			
			socketToken.currentCommand = command;
			socketToken.currentBytes = data;
			socketToken.AsyncCallback = Callback;
			socketToken.sendArgs.SetBuffer(data, 0, data.Length);
			if (!socketToken.socket.SendAsync(socketToken.sendArgs)) OnSend(socketToken.socket, socketToken.sendArgs); 
		}
		
		
		/*
			handlers
		*/
		private void OnConnected (object unused, SocketAsyncEventArgs args) {
			var token = (SocketToken)args.UserToken;
			switch (token.socketState) {
				case SocketState.OPENING: {
					if (args.SocketError != SocketError.Success) {
						token.socketState = SocketState.CLOSED;
						var error = new Exception("connect error:" + args.SocketError.ToString());
						
						ConnectionFailed(this, error);
						return;
					}
					
					token.socketState = SocketState.OPENED;
					
					// // ready receive data.
					// token.socket.ReceiveAsync(token.receiveArgs);// この行の内容を、Loop設定時にすれば良い。
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
					Disquuun.Log("まだCloseハンドルしてない");
					// if (Closed != null) Closed(this.connectionId);
					break;
				}
			}
		}
		
		private void OnSend (object unused, SocketAsyncEventArgs args) {
			var socketError = args.SocketError;
			switch (socketError) {
				case SocketError.Success: {
					// do nothing.
					break;
				}
				default: {
					Disquuun.Log("まだエラーハンドルしてない。切断の一種なんだけど、非同期実行してるAPIに紐付けることができる。");
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
						Disquuun.Log("まだエラーハンドルしてない2。切断の一種なんだけど、非同期実行してるAPIに紐付けることができる、、、かなあ？　できない気もしてきたぞ。");
						// if (Error != null) {
						// 	var error = new Exception("receive error:" + args.SocketError.ToString() + " size:" + args.BytesTransferred);
						// 	Error(error);
						// }
						
						// connection is already closed.
						if (!IsSocketConnected(token.socket)) {
							Disconnect();
							return;
						}
						
						// continue receiving data. go to below.
						break;
					}
				}
			}
			
			if (0 < args.BytesTransferred) {
				var dataSource = args.Buffer;
				var bytesAmount = args.BytesTransferred;
				
				var rest = args.AcceptSocket.Available;
				if (0 < rest) {
					var restBuffer = new byte[rest];
					var additionalReadResult = token.socket.Receive(restBuffer, SocketFlags.None);// ここでロックしちゃう気がする。が、asyncに変えればなんとか逃げられそう。
					TestLogger.Log("byteTransferredか、指定bufferサイズを超えた場合にここに来る気がする。");
					
					var baseLength = dataSource.Length;
					Array.Resize(ref dataSource, baseLength + rest);
					
					for (var i = 0; i < rest; i++) dataSource[baseLength + i] = restBuffer[i];
					bytesAmount = dataSource.Length;
				}
				
				// 読み出そうとして、そのタイミングでデータの全体像が整ってない、っていうのがあり得る。
				// データが足りてない場合、この辺でロックしてしまうのはマズイよな。
				// ・足りているかどうかの仮読みはやっぱり欲しいわけだな。っていうかデータだけをindexで取り出せれば良いんだよな。
				
				TestLogger.Log("asyncはまだ対応してない。");
				
				// var result = DisquuunAPI.EvaluateSingleCommand(token.currentCommand, dataSource);
				// var continuation = token.AsyncCallback(token.currentCommand, result);
				
				// if (continuation) {
				// 	// ready for receive.
				// 	if (!socketToken.socket.ReceiveAsync(socketToken.receiveArgs)) OnReceived(socketToken.socket, socketToken.receiveArgs);
			
				// 	socketToken.sendArgs.SetBuffer(token.currentBytes, 0, token.currentBytes.Length);
				// 	if (!token.socket.SendAsync(token.sendArgs)) OnSend(token.socket, token.sendArgs);
				// } else {
				// 	token.socketState = SocketState.OPENED;
				// }
			}
		}
		
		
		public void Disconnect (bool force=false) {
			if (force) {
				try {
					socketToken.socket.Close();
				} catch (Exception e) {
					Disquuun.Log("e:" + e);
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
					
					var closeEventArgs = new SocketAsyncEventArgs();
					closeEventArgs.UserToken = socketToken;
					closeEventArgs.AcceptSocket = socketToken.socket;
					closeEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnClosed);
					
					if (!socketToken.socket.DisconnectAsync(closeEventArgs)) OnClosed(socketToken.socket, closeEventArgs);
					break;
				}
			}
		}
		
		
		/*
			utils
		*/
		
		private static bool IsSocketConnected (Socket s) {
			bool part1 = s.Poll(1000, SelectMode.SelectRead);
			bool part2 = (s.Available == 0);
			
			if (part1 && part2) return false;
			
			return true;
		}
	}
	
	
	public static class DisquuunExtension {
		public static DisquuunResult[] Sync (this DisquuunInput input) {	
			var socket = input.socket;
			return socket.Sync(input.command, input.data);
		}
		
		public static void Async (this DisquuunInput input, Action<DisqueCommand, DisquuunResult[]> Callback) {	
			var socket = input.socket;
			socket.Async(
				input.command, 
				input.data, 
				(command, resultBytes) => {
					Callback(command, resultBytes);
					return false;
				}
			);
		}
		
		public static void Loop (this DisquuunInput input, Func<DisqueCommand, DisquuunResult[], bool> Callback) {	
			var socket = input.socket;
			socket.Loop(input.command, input.data, Callback);
		}
	}
}