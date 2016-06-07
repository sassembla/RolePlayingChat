using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

namespace XrossPeerUtility {
    public class XrossPeer {
		static string logPath = string.Empty;
		
		private static Action<string> logAction;
		
		public static void SetupLog (string logOutputPath=null, Action<string> LogAct=null) {
			if (!string.IsNullOrEmpty(logOutputPath)) logPath = logOutputPath;
			if (LogAct == null) logAction = WriteLog;
		}
		
		public static void Log (string message) {
			logAction(message);
		}
		
		public static void LogWarning (string message) {
			logAction("WARNING:" + message);
		}
		
		public static void LogError (string message) {
			logAction("ERROR:" + message);
			logAction("stacktrace:" + Environment.StackTrace);
		}
		
		

		public static void WriteLog (string message) {
			Assert(!string.IsNullOrEmpty(logPath), "xrosspeer output path is empty.");

			// // file write
			// using (var fs = new FileStream(
			// 	logPath,
			// 	FileMode.Append,
			// 	FileAccess.Write,
			// 	FileShare.ReadWrite)
			// ) {
			// 	using (var sr = new StreamWriter(fs)) {
			// 		sr.WriteLine("log:" + message);
			// 	}
			// }
		}
		
		

		public static void LogDict (string message, Dictionary<string, string> dictObj, string peerId="undef") {
			WriteLog("log:dict:" + message + " count:" + dictObj.Count);
			foreach (var key in dictObj.Keys) {
				WriteLog("	key:" + key + " val:" + dictObj[key]);
			}
		}
		
		public static void LogList (string message, List<string> list, string peerId="undef") {
			WriteLog("log:list:" + message + " count:" + list.Count);
			foreach (var obj in list) {
				WriteLog("	val:" + obj);
			}
		}
				
		/**
			assert which extends bool.
			
			e.g.
				Assert(false, "hereComes");

				will fail.

			Assert(mustNotNull != null, "but null,,");
		*/
		public static void Assert (bool condition, string reason) {
			if (condition) return;

			OutputStackThenDown(reason);
		}


		/**
			assert which extends string of date
		
			e.g.
				TimeAssert("2014/07/08 00:00:00", "hereComes");

				will fail after 2014/07/08 00:00:00.

			TimeAssert("2014/07/07 00:00:00", "time's up!");
		*/
		public static void TimeAssert (string limitDate, string reason, int additionalSec = 0) {
			DateTime parsedDate;
			
			var fullhead_time_result = DateTime.TryParseExact(limitDate, "yyyy/MM/dd hh:mm:ss", null, DateTimeStyles.None, out parsedDate);
			if (!fullhead_time_result) {
				var no_head_time_result = DateTime.TryParseExact(limitDate, "yyyy/MM/dd h:mm:ss", null, DateTimeStyles.None, out parsedDate);
				if (!no_head_time_result) {
					// WriteLog("assertion passed:" + reason + ", until " + limitDate);
					return;
				}
			}

			var now = DateTime.Now;
			var diff = now - parsedDate;
			var diffSec = Math.Floor(diff.TotalSeconds);
			
			if (diffSec < additionalSec) {
				return;
			}

			OutputStackThenDown(reason + " passed:" + (additionalSec - diffSec) + "sec");
		}

		/**
			utility.
		*/
		private static void OutputStackThenDown (string reason) {
			System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(true);

			// at least 2 stack exists in st. 0 is "System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(true);", 1 is where the assertion faild.
			var assertFaildPointDescription = st.GetFrame(2).ToString();

			// get specific data from stacktrace.
			var descriptions = assertFaildPointDescription.Split(':');
			var fileName = descriptions[2].Split(' ')[1];
			var line = descriptions[3];
			
			LogError("assertion failed:" + fileName + ":" + line + ":" + reason);
		}
	}
	
}