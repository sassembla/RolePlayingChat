using System;
using System.Net.Sockets;
using DisquuunCore;

public class External {
    public static void Disconnect(DisquuunSocket.SocketToken socketToken, Action<object, SocketAsyncEventArgs> OnClosed) {
		if (OnClosed == null) {
			socketToken.socket.Close();
			return;
		}

        var closeEventArgs = new SocketAsyncEventArgs();
		closeEventArgs.UserToken = socketToken;
		closeEventArgs.AcceptSocket = socketToken.socket;
		closeEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnClosed);
		
		if (!socketToken.socket.DisconnectAsync(closeEventArgs)) OnClosed(socketToken.socket, closeEventArgs);
    }
}