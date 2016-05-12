using UnityEngine;
using System;
using System.Collections.Generic;
using DisquuunCore;
using System.IO;
using System.Threading;

public class DisquuunTests {
	public static Tests tests;
	
	public static void Start () {
		tests = new Tests();
		tests.RunTests();	
	}
	
	public static void Stop () {
		if (tests != null) tests.Teardown();
		tests = null;
	}
}


public partial class Tests {
	public Disquuun disquuun;
	
	public void RunTests () {
		
		var tests = new List<Action>();
		tests.Add(_0_0_InitWith2Connection);
		tests.Add(_0_1_AddLoopSlot);
		
		foreach (var test in tests) {
			Setup();
			test();
			Teardown();
		}
	}
	
	public void Setup () {
		disquuun = new Disquuun("127.0.0.1", 7711, 10240, 2);
	}
	
	public void Teardown () {
		if (disquuun != null) disquuun.Disconnect();
	}
	
	
	
	public class WaitThreadClass {
		public ManualResetEvent e;
		public readonly Func<bool> Until;
		public readonly string methodName;
		
		public readonly int timeout;
		
		
		public WaitThreadClass (Func<bool> Until, string methodName, int timeout) {
			this.Until = Until;
			this.methodName = methodName;
			this.timeout = timeout;	
			e = new ManualResetEvent(false);
		}
		
		public void Exec () {
			e.Reset();
			
			int count = 0;
			var startTime = DateTime.Now;
			
			
			try {
				while (!Until()) {
					var current = DateTime.Now;
					var distanceSeconds = (current - startTime).Seconds;
					
					if (timeout < distanceSeconds) {
						Debug.LogError("timeout:" + methodName);
						break;
					}
					
					System.Threading.Thread.Sleep(10);
					
					count++;
				}
			} catch (Exception e2) {
				Debug.LogError("e2:" + e2);
			}
			
			e.Set();
		}
	}
	
	public void WaitFor (Func<bool> waitFor, int timeoutSec) {
		System.Diagnostics.StackTrace stack  = new System.Diagnostics.StackTrace(false);
		var methodName = stack.GetFrame(1).GetMethod().Name;
		
		var waitThreadClass = new WaitThreadClass(waitFor, methodName, timeoutSec);
		
		var waitingThread = new Thread(
			new ThreadStart(waitThreadClass.Exec)
		);
		waitingThread.Start();
		waitThreadClass.e.WaitOne();
	}
	
	
	public static void TestLog (string message) {
		System.Diagnostics.StackTrace stack  = new System.Diagnostics.StackTrace(false);
		var methodName = stack.GetFrame(3).GetMethod().Name;
		
		TestLogger.Log("failed:" + methodName + " message:" + message);
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