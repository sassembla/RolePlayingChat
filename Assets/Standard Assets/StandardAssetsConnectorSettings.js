#pragma strict
import System.IO;


class StandardAssetsConnectorSettings extends ScriptableObject {
	private var domainKey = "ws://127.0.0.1:80/";
	private var gameKey = "calivers_" + "disque" + "_client";

	// true | false
	private var use_private = true;

	// ["redis", "disque"]
	private var queue_type = "disque";

	// ["binary, string"]
	public var data_mode = "binary";

	// true | false
	public var use_unity_thread = false;

	/*
		constants
	*/
	private var REDIS_PUBLISHKEY_FOOTER = "_pub";
	private var REDIS_SUBSCRIBEKEY_FOOTER = "_sub";
	private var DISQUE_QUEUE_FOOTER = "_context";


	/*
		functions(maybe no need to modify.)
	*/
	public function GetAssetPath () {
		return Application.dataPath;
	}
	
	public function ContextToClientKey () {
		switch (queue_type) {
			case "disque": 
				return "nothing.";
			case "redis": 
				if (use_private) return gameKey + GetPrivateClientKey() + REDIS_PUBLISHKEY_FOOTER;
				
				return gameKey + REDIS_PUBLISHKEY_FOOTER;
		}
		return "no queue_type found:" + queue_type;
	}

	public function ClientToContextKey () {
		switch (queue_type) {
			case "disque": 
				if (use_private) return gameKey + GetPrivateClientKey() + DISQUE_QUEUE_FOOTER;

				return gameKey + DISQUE_QUEUE_FOOTER;
			case "redis":
				if (use_private) return gameKey + GetPrivateClientKey() + REDIS_SUBSCRIBEKEY_FOOTER;

				return gameKey + REDIS_SUBSCRIBEKEY_FOOTER;
		}
		return "no queue_type found:" + queue_type;
	}

	public function DataMode () {
		switch (queue_type) {
			case "disque": 
				return queue_type + "_" + data_mode;
			case "redis": 
				return queue_type + "_" + data_mode;
		}
		return "no queue_type found:" + queue_type;
	}

	public function QueueType () {
		return queue_type;
	}

	public function DomainKey () {
		return domainKey;
	}

	public function ClientKey () {
		if (!use_private) return gameKey;

		var clientKeyObj = ScriptableObject.CreateInstance("StandardAssetsPrivateClientKey");
		if (clientKeyObj) {
			generatedKey = clientKeyObj.ToString();
			return gameKey + generatedKey;
		}

		Debug.LogError("failed to get client key.");
		return "error";
	}

	/**
		return client-pub key.
	*/
	private var generatedKey = "unset";
	private function  GetPrivateClientKey () {
		if (generatedKey != "unset") return generatedKey;

		
		var standartAssetPath = Path.Combine(GetAssetPath(), "Standard Assets");
		var randomKeyPath = Path.Combine(standartAssetPath, "StandardAssetsPrivateClientKey.js");
	

		if (File.Exists(randomKeyPath)) {
			var clientKeyObj = ScriptableObject.CreateInstance("StandardAssetsPrivateClientKey");
			generatedKey = clientKeyObj.ToString();
			return generatedKey;
		}

		generatedKey = GeneratePrivateClientKey();

		return generatedKey;
	}

	/**
		generate new clinet-pub key then store it.
	*/
	public function GeneratePrivateClientKey () {
		var standartAssetPath = Path.Combine(GetAssetPath(), "Standard Assets");
		var randomKeyPath = Path.Combine(standartAssetPath, "StandardAssetsPrivateClientKey.js");
	
		var key = System.Guid.NewGuid().ToString();

		var sw : StreamWriter = new StreamWriter(randomKeyPath);
		var javascriptStruct = 
			"#pragma strict" + "\n"
			 + "class StandardAssetsPrivateClientKey extends ScriptableObject {" + "\n"
			 + "public function ToString () { return \"" + key + "\"; }" + "\n"
			 + "}" + "\n";

		sw.WriteLine(javascriptStruct);
		sw.Flush();
		sw.Close();

		#if UNITY_EDITOR
		{
			UnityEditor.AssetDatabase.Refresh();
		}
		#endif

		return key;
	}

}
