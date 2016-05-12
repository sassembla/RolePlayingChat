#pragma strict
import System.IO;


class StandardAssetsConnectorSettings extends ScriptableObject {
	private var domainKey = "ws://127.0.0.1:80/";
	private var gameKey = "roleplayingchat_" + "disque" + "_client";

	// true | false
	private var use_private = true;

	/*
		constants
	*/
	private var DISQUE_QUEUE_FOOTER = "_context";


	/*
		functions(maybe no need to modify.)
	*/
	public function GetAssetPath () {
		return Application.dataPath;
	}

	public function ClientToContextKey () {
		if (use_private) return gameKey + GetPrivateClientKey() + DISQUE_QUEUE_FOOTER;
		return gameKey + DISQUE_QUEUE_FOOTER;
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
