using XrossPeerUtility;

using System;
using System.Threading;


public class Updater {
	private bool continuation;
	/*
		C#のtickで動くThread。
		UnityEditorのUpdateと違い、UnityPlayerと同期しないため、
		UnityPlayer側が重くなっても構わず動く。

		で、UnityPlayer側が遅れてかつUnityServer側が動くと、unsyncエラーが多発する。
		このUnity内でのUnityPlayerと合わせてUnityEditor上で使うには都合が悪い。

		別のUnityPlayerに対してのServerインスタンスとしては行けると思う。
		あとはCoreCLRにするとか。
	*/
	public Updater (string loopId, Func<bool> OnUpdate) {
		var framePerSecond = RolePlayingChatDefinitions.FRAMERATE;
		var mainThreadInterval = 1000f / framePerSecond;
		
		continuation = true;
		
		Action loopMethod = () => {
			try {
				double nextFrame = (double)System.Environment.TickCount;
				
				var before = 0.0;
				var tickCount = (double)System.Environment.TickCount;
				
				while (true) {
					tickCount = System.Environment.TickCount * 1.0;
					if (nextFrame - tickCount > 1) {
						// XrossPeer.Log("wait:" + (int)(nextFrame - tickCount));
						Thread.Sleep((int)(nextFrame - tickCount)/2);
						/*
							waitを半分くらいにすると特定フレームで安定した。
						*/
						continue;
					}
					
					if (tickCount >= nextFrame + mainThreadInterval) {
						nextFrame += mainThreadInterval;
						continue;
					}
					
					if (!continuation) break;
					
					// run action for update.
					continuation = OnUpdate();
					if (!continuation) break;
					
					nextFrame += mainThreadInterval;
					before = tickCount; 
				}
				XrossPeer.Log("loopId:" + loopId + " is finished.");
			} catch (Exception e) {
				XrossPeer.LogError("loopId:" + loopId + " error:" + e);
			}
		};
		
		var thread = new Thread(new ThreadStart(loopMethod));
		thread.Start();
	}
	
	public void Quit () {
		continuation = false;
	}
}