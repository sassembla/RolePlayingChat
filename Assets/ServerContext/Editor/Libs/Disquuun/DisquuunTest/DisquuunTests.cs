using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using DisquuunCore;
using DisquuunCore.Deserialize;

public class DisquuunTests {
	public static Tests tests;
	
	public static void Start () {
		tests = new Tests();
		tests.RunTests();	
	}
	
	public static void Stop () {
		tests = null;
	}
}


public partial class Tests {
	public void RunTests () {
		var tests = new List<Action<Disquuun>>();
		
		// basement.
		tests.Add(_0_0_InitWith2Connection);
		tests.Add(_0_0_1_WaitOnOpen2Connection);
		tests.Add(_0_0_2_ReadmeSample);
		tests.Add(_0_1_ConnectionFailedWithNoDisqueServer);
		tests.Add(_0_2_SyncInfo);
		tests.Add(_0_3_SyncInfoTwice);
		tests.Add(_0_4_AsyncInfo);
		tests.Add(_0_5_LoopInfo_Once);
		tests.Add(_0_6_LoopInfo_Twice);
		tests.Add(_0_7_LoopInfo_100);
		
		// sync apis. DEPRECATED.
		tests.Add(_1_0_AddJob_Sync);
		tests.Add(_1_1_GetJob_Sync);
		tests.Add(_1_1_1_GetJobWithCount_Sync);
		tests.Add(_1_1_2_GetJobFromMultiQueue_Sync);
		tests.Add(_1_1_3_GetJobWithNoHang_Sync);
		tests.Add(_1_2_AckJob_Sync);
		tests.Add(_1_3_Fastack_Sync);
		
		// async apis.
		tests.Add(_2_0_AddJob_Async);
		tests.Add(_2_1_GetJob_Async);
		tests.Add(_2_1_1_GetJobWithCount_Async);
		tests.Add(_2_1_2_GetJobFromMultiQueue_Async);
		tests.Add(_2_1_3_GetJobWithNoHang_Async);
		tests.Add(_2_2_AckJob_Async);
		tests.Add(_2_3_Fastack_Async);
		
		// multiSocket.
		tests.Add(_3_0_2AsyncSocket);
		tests.Add(_3_1_MultipleAsyncSocket);
		
		// buffer over.
		tests.Add(_4_0_ByfferOverWithSingleSyncGetJob_Sync);
		tests.Add(_4_1_ByfferOverWithMultipleSyncGetJob_Sync);
		tests.Add(_4_2_ByfferOverWithSokcetOverSyncGetJob_Sync);
		tests.Add(_4_3_ByfferOverWithSingleSyncGetJob_Async);
		tests.Add(_4_4_ByfferOverWithMultipleSyncGetJob_Async);
		tests.Add(_4_5_ByfferOverWithSokcetOverSyncGetJob_Async);
		
		// error handling.
		// tests.Add(_5_0_Error)// connect時に出るエラー、接続できないとかその辺。
		
		// adding async request over busy-socket num.
		// tests.Add(_6_0_ExceededSocketNo3In2);
		
		
		TestLogger.Log("tests started.");
		
		foreach (var test in tests) {
			try {
				var disquuun = new Disquuun("127.0.0.1", 7711, 2020008, 2);
				test(disquuun);
				if (disquuun != null) {
					disquuun.Disconnect(true);
					disquuun = null;
				}
			} catch (Exception e) {
				TestLogger.Log("test:" + test + " FAILED by exception:" + e);
			}
		}
		
		var restJobCount = -1;
		
		var disquuun2 = new Disquuun("127.0.0.1", 7711, 10240, 1);
		WaitUntil(() => (disquuun2.State() == Disquuun.ConnectionState.OPENED), 5);
		disquuun2.Info().Async(
			(command, data) => {
				var result = DisquuunDeserializer.Info(data);
				
				if (result.jobs != null) restJobCount = result.jobs.registered_jobs;
				TestLogger.Log("all tests over. rest unconsumed job:" + restJobCount);
			}
		);
		
		WaitUntil(() => (restJobCount != -1), 5);
		
		disquuun2.Disconnect(true);
	}
	
	
	public void WaitUntil (Func<bool> WaitFor, int timeoutSec) {
		System.Diagnostics.StackTrace stack  = new System.Diagnostics.StackTrace(false);
		var methodName = stack.GetFrame(1).GetMethod().Name;
		var resetEvent = new ManualResetEvent(false);
		
		var waitingThread = new Thread(
			() => {
				resetEvent.Reset();
				var startTime = DateTime.Now;
				
				try {
					while (!WaitFor()) {
						var current = DateTime.Now;
						var distanceSeconds = (current - startTime).Seconds;
						
						if (timeoutSec < distanceSeconds) {
							TestLogger.Log("timeout:" + methodName);
							break;
						}
						
						System.Threading.Thread.Sleep(10);
					}
				} catch (Exception e) {
					TestLogger.Log("methodName:" + methodName + " error:" + e);
				}
				
				resetEvent.Set();
			}
		);
		
		waitingThread.Start();
		
		resetEvent.WaitOne();
	}
	
	public void Assert (bool condition, string message) {
		System.Diagnostics.StackTrace stack  = new System.Diagnostics.StackTrace(false);
		var methodName = stack.GetFrame(1).GetMethod().Name;
		if (!condition) TestLogger.Log("test:" + methodName + " FAILED:" + message); 
	}
	
	public void Assert (object expected, object actual, string message) {
		System.Diagnostics.StackTrace stack  = new System.Diagnostics.StackTrace(false);
		var methodName = stack.GetFrame(1).GetMethod().Name;
		if (expected.ToString() != actual.ToString()) TestLogger.Log("test:" + methodName + " FAILED:" + message + " expected:" + expected + " actual:" + actual); 
	}
}




public static class TestLogger {
	public static string logPath;
	
	public static void Log (string message) {
		logPath = "test.log";
		
		// file write
		using (var fs = new FileStream(
			logPath,
			FileMode.Append,
			FileAccess.Write,
			FileShare.ReadWrite)
		) {
			using (var sr = new StreamWriter(fs)) {
				sr.WriteLine("log:" + message);
			}
		}
	}
}