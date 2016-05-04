using System;
using System.Linq;
using System.Threading;
using DisquuunCore;
using DisquuunCore.Deserialize;

public class TestBase {
	public bool waiting;
	
	private int index;
	
	public string latestAddedJobId;
	public string latestGotJobId;
	public string[] latestWholeGotJobId;
	
	public string latestResult;
	
	public string latestError;
	
	public string jobQueueId;

	public Action[] acts;
	
	public Disquuun disquuun;
	public Disquuun disquuun2;
	
	public TestLogger testLogger;
	
	
	public TestBase () {
		testLogger = new TestLogger();
		
		disquuun2 = new Disquuun(
			Guid.NewGuid().ToString(),
			"127.0.0.1",
			7711,
			5000,
			(connectionId) => {
				Setup();
			},
			(command, byteDatas) => {
				JobProcess(command, byteDatas);
			}
		);
	}
	
	private void Setup () {
		var conId = Guid.NewGuid().ToString();
		
		disquuun = new Disquuun(
			conId,
			"127.0.0.1",
			7711,
			102400,
			(connectionId) => {
				acts = Ready(connectionId);
				index = 0;
				new TestUpdater("disquuunTestThread_" + connectionId, Run);
			},
			(command, byteDatas) => {
				JobProcess(command, byteDatas);
			},
			(failedCommand, reason) => {
				// testLogger.Log("failedCommand:" + failedCommand + " reason:" + reason);
				latestError = reason;
			}
		);
	}
	
	
	
	public virtual Action[] Ready (string testSuiteId) {
		return null;
	}
	
	public virtual void JobProcess (Disquuun.DisqueCommand command, Disquuun.ByteDatas[] data) {
		// do nothing
	}
	
	private bool Run() {
		if (index < acts.Length) {
			if (!waiting) {
				acts[index]();
				index++;
			}
		} else {
			return false;
		}
		
		// testLogger.Log("incremented:" + index);
		return true;
	}
	
	public void AssertResult(string expectedJobResult, string actualLatestJobResult, string message) {
		if (expectedJobResult == actualLatestJobResult) ;//testLogger.Log("PASSED:" + message);
		else {
			var error = "FAILED:" + message + " expected:" + expectedJobResult + " actual:" + actualLatestJobResult;
			testLogger.Log(error);
			// throw new Exception(error);
		}
	}
	
	public void AssertFailureResult(string expectedJobFailedResult, string actualLatestJobFailedResult, string message) {
		if (expectedJobFailedResult == actualLatestJobFailedResult) ;//testLogger.Log("PASSED:" + message);
		else {
			var error = "FAILED:" + message + " actual:" + actualLatestJobFailedResult;
			testLogger.Log(error);
			// throw new Exception(error);
		}
	}
	
	public class TestUpdater {
		public TestUpdater (string loopId, Func<bool> OnUpdate) {
			var mainThreadInterval = 1000f / 60;
			var testLogger = new TestLogger();
			var errorBreak = false;
			Action loopMethod = () => {
				try {
					double nextFrame = (double)System.Environment.TickCount;
					
					var before = 0.0;
					var tickCount = (double)System.Environment.TickCount;
					
					while (true) {
						tickCount = System.Environment.TickCount * 1.0;
						if (nextFrame - tickCount > 1) {
							Thread.Sleep(100);
							continue;
						}
						
						if (tickCount >= nextFrame + mainThreadInterval) {
							nextFrame += mainThreadInterval;
							continue;
						}
						
						// run action for update.
						var continuation = OnUpdate();
						if (!continuation) break;
						if (errorBreak) break;
						nextFrame += mainThreadInterval;
						before = tickCount; 
					}
					testLogger.Log("loopId:" + loopId + " is finished.");
				} catch (Exception e) {
					testLogger.LogError("loopId:" + loopId + " error:" + e);
					errorBreak = true;
				}
			};
			
			var thread = new Thread(new ThreadStart(loopMethod));
			thread.Start();
		}
	}
}