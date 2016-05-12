using System;
using System.Threading;
using DisquuunCore;

public partial class Tests {
	public void _0_0_InitWith2Connection () {
		var state = disquuun.State();
		if (state == Disquuun.ConnectionState.ALLCLOSED) return;
		TestLog("state mismatch.");
	}
	
	public void _0_1_AddLoopSlot () {
		
		WaitFor(
			() => false,//disquuun.State() == Disquuun.ConnectionState.OPENED
			2 
		);
		
		TestLog("not yet applied.");
	}

    private void waiting()
    {
        throw new NotImplementedException();
    }

    private void waiting (Func<bool> a) { 
        while (a()) {
			
		}
    }

    public void _0_2_AddJob () {
		TestLog("not yet applied.");
	}
}