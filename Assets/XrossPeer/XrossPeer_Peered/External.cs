using System;
using System.Net.Sockets;
using DisquuunCore;

public class External {
    public static void Disconnect(DisquuunSocket.SocketToken socketToken, Action<object, SocketAsyncEventArgs> OnClosed) {
    	// socketToken.socket.Shutdown(SocketShutdown.Both);
		// socketToken.socket.Dispose();// あれ、、動きそうだな、、まあ気持ち悪いから消しておこう。
    }
}