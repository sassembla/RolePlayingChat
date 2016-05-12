using DisquuunCore;

public partial class Tests {
	public void _0_0_InitWith2Connection (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
	}

    public void _0_1_Sync () {
		TestLogger.Log("not yet applied.");
	}
}