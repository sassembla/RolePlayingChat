using System;
using DisquuunCore;
using DisquuunCore.Deserialize;

public partial class Tests {
	public void _0_0_InitWith2Connection (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
	}

    public void _0_1_ConnectionFailedWithNoDisqueServer (Disquuun disquuun) {
		Exception e = null;
		Action<Exception> Failed = (Exception e2) => {
			// set error to param,
			e = e2;
			// TestLogger.Log("e:" + e);
		};
		
		var disquuun2 = new Disquuun("127.0.0.1", 8888, 1024, 1, Failed);
		
		WaitUntil(() => (e != null), 1);
		
		disquuun2.Disconnect(true);
	}
	
	public void _0_2_SyncInfo (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		var data = disquuun.Info().Sync();
		var infoStr = DisquuunDeserializer.Info(data);
		// なんかバリデーションしないとな〜
	}
	
	public void _0_3_SyncInfoTwice (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		{
			var datas = disquuun.Info().Sync();
			var infoStr = DisquuunDeserializer.Info(datas);
			// なんかバリデーションしないとな〜
		}
		
		{
			var datas = disquuun.Info().Sync();
			var infoStr = DisquuunDeserializer.Info(datas);
			// なんかバリデーションしないとな〜
		}	
	}
	
	public void _0_4_AsyncInfo (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var infoStr = string.Empty;
		disquuun.Info().Async(
			(DisqueCommand command, DisquuunResult[] datas) => {
				TestLogger.Log("hereComes");
				infoStr = DisquuunDeserializer.Info(datas);
			}
		);
		
		WaitUntil(() => !string.IsNullOrEmpty(infoStr), 5);
	}
	
	// public void _0_5_LoopInfo (Disquuun disquuun) {
	// 	WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
	// 	var infoStr = string.Empty;
	// 	disquuun.Info().Loop(
	// 		(DisqueCommand command, DisquuunResult[] datas) => {
	// 			TestLogger.Log("hereComes");
	// 			infoStr = DisquuunDeserializer.Info(datas);
	// 			return true;
	// 		} 
	// 	);
		
	// 	WaitUntil(() => !string.IsNullOrEmpty(infoStr), 5);
	// }
	
}