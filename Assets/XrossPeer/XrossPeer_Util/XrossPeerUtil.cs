using System;
using System.IO;
using System.Text;

namespace XrossPeerLogger {
    public class XrossPeer {
		private readonly string peerIdentity;
		
		private static XrossPeer xrossPeer;
		
		public static void SetupLogger (string peerIdentity) { 
			xrossPeer = new XrossPeer(peerIdentity);
		}

		private XrossPeer (string peerIdentity) {
			this.peerIdentity = peerIdentity;
		}
		
		public static void Log (string message) {
			if (xrossPeer == null || string.IsNullOrEmpty(xrossPeer.peerIdentity)) throw new Exception("should set peer identity with XrossPeer.Setup(PEER_IDENTITY)");
			xrossPeer.WriteLog(message);
		}
		
		public static void LogWarning (string message) {
			if (xrossPeer == null || string.IsNullOrEmpty(xrossPeer.peerIdentity)) throw new Exception("should set peer identity with XrossPeer.Setup(PEER_IDENTITY)");
			xrossPeer.WriteLog("WARNING:" + message);
		}
		
		public static void LogError (string message) {
			if (xrossPeer == null || string.IsNullOrEmpty(xrossPeer.peerIdentity)) throw new Exception("should set peer identity with XrossPeer.Setup(PEER_IDENTITY)");
			xrossPeer.WriteLog("ERROR:" + message);
			xrossPeer.WriteLog("stacktrace:" + Environment.StackTrace);// これつかえないんじゃねーかな、、
		}
		
		private void WriteLog (string message, bool writeout=false) {
			Logger.Log(peerIdentity, message, writeout);
		}
	}
	
	public static class Logger {
		private static object lockObject = new object();
		public static StringBuilder logs = new StringBuilder();
		public static void Log (string logPath, string message, bool writeout=false) {
			lock (lockObject) {
				if (!writeout) {
					logs.AppendLine(message);
					return;
				}

				// file write
				using (var fs = new FileStream(
					logPath,
					FileMode.Append,
					FileAccess.Write,
					FileShare.ReadWrite)
				) {
					using (var sr = new StreamWriter(fs)) {
						if (0 < logs.Length) {
							sr.WriteLine(logs.ToString());
							logs = new StringBuilder();// note that 
						}
						
						sr.WriteLine("peer:" + logPath + " log:" + message);
					}
				}
			}
		}
	}


}