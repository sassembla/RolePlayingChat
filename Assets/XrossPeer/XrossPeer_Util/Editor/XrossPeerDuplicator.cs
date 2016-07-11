using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System;


namespace XrossPeerUtility {
	public class XrossPeerDuplicator {
		public const string XROSSPEER_NOT_PATH_DELIM = "\\";
		public const string XROSSPEER_PATH_DELIM = "/";
		
		[MenuItem ("XrossPeer/Compare", false, 1)] static void DoCompare () {
			Compare();
		} 
		
		private static bool Compare (bool showInfo=true) {
			var noDiff = true;
			
			if (showInfo) Debug.Log("comparing XrossPeers...");

			var clientFolderPath = Search_CrossPeerFolder();
			if (string.IsNullOrEmpty(clientFolderPath)) {
				if (showInfo) Debug.Log("failed to detect clientside XrossPeer folder.");
				return false;
			}
			

			var serverFolderPath = SearchEditor_CrossPeerFolder();
			if (string.IsNullOrEmpty(serverFolderPath)) {
				if (showInfo) Debug.Log("failed to detect serverside XrossPeer folder.");
				return false;
			}
			

			var clientFileDates = GetAllFilesDict(clientFolderPath);
			var serverFileDates = GetAllFilesDict(serverFolderPath);


			// from client to server comparison
			foreach (var path in clientFileDates.Keys) {
				if (!serverFileDates.ContainsKey(path)) {
					if (showInfo) Debug.LogWarning("server-side xrossPeer folder does not contains:" + path.Replace(".cs", ".server.cs"));
					noDiff = false;
					continue;
				}
				
				// both exists. check data.
				if (clientFileDates[path].hash != serverFileDates[path].hash) {
					if (showInfo) Debug.LogError("diff exists between client & server. File:" + path.Replace(".cs", ".server | client.cs"));
					var clientIsNew = string.Empty;
					var serverIsNew = string.Empty;
					if (0 < clientFileDates[path].lastWriteTime.CompareTo(serverFileDates[path].lastWriteTime)) {
						clientIsNew = " -latest-";
					} else {
						serverIsNew = " -latest-";
					} 
					if (showInfo) {
						Debug.LogError("	client	updated at:" + clientFileDates[path].lastWriteTime + clientIsNew);
						Debug.LogError("	server	updated at:" + serverFileDates[path].lastWriteTime + serverIsNew);
					}
					noDiff = false;
				}
			}

			// from server to client comparison
			foreach (var path in serverFileDates.Keys) {
				if (!clientFileDates.ContainsKey(path)) {
					if (showInfo) Debug.LogWarning("client-side xrossPeer folder does not contains:" + path.Replace(".cs", ".client.cs"));
					noDiff = false;
					continue;
				}
			}

			if (showInfo) Debug.Log("comparison fihished.");
			return noDiff;
		}

		private static Dictionary<string, FileDatas> GetAllFilesDict (string baseFolderPath) {
			var dict = new Dictionary<string, FileDatas>();
			
			DirectorySearch(dict, baseFolderPath, baseFolderPath);
			
			return dict;
		}
		
		static void DirectorySearch(Dictionary<string, FileDatas> dict, string baseDirPath, string ignoreBasePath) {
			FileSearch(dict, baseDirPath, ignoreBasePath);
			
			var dirs = Directory.GetDirectories(baseDirPath);
			if (!dirs.Any()) return;

			foreach (string dirPath in dirs) DirectorySearch(dict, dirPath, ignoreBasePath);
		}

		/*
			この辺が1:1なのなんとかしたい。AtoBに一般化すればいいか。
			まあGUIから叩くようにしよう。このメソッドが生き残ることは多分ない。
		*/
		static void FileSearch (Dictionary<string, FileDatas> dict, string baseDirPath, string ignoreBasePath) {
			foreach (string filePath in Directory.GetFiles(baseDirPath)) {
				if (filePath.EndsWith(".meta")) continue;
				
				var fileName = Path.GetFileName(filePath);
				if (fileName.StartsWith(".")) continue;
				
				// ignore base path
				var basePathIgnoredFilePath = filePath.Replace(ignoreBasePath, string.Empty);
				
				// ignore file extension. e.g. A.client.cs -> A.cs, B.server.cs -> B.cs
				var extentionIgnoredFilePath = basePathIgnoredFilePath.Replace(".client.", ".").Replace(".server.", ".");
				
				using (var md5 = MD5.Create()) 
				using (var sr = new StreamReader(filePath)) {
					var codes = sr.ReadToEnd();
					var bytes = Encoding.UTF8.GetBytes(codes);
					var hash = md5.ComputeHash(bytes);
					
					dict[extentionIgnoredFilePath] = new FileDatas(string.Join("-", hash.Select(b => b.ToString()).ToArray()), File.GetLastWriteTime(filePath));
				}
			}
		}
		
		[MenuItem ("XrossPeer/DuplicateFrom_Client_To_Server", false, 1)] static void DuplicateC2S () {
			var sourcePath = Search_CrossPeerFolder();
			var destPath = SearchEditor_CrossPeerFolder();

			CopyAllFilesWithoutMeta(new Component(sourcePath), new Component(destPath), true);
			EditorApplication.ExecuteMenuItem("Assets/Refresh");
		}
		[MenuItem ("XrossPeer/DuplicateFrom_Client_To_Server", true)] static bool IsComparedForClient () {
			return !Compare(false);
		}

		
		[MenuItem ("XrossPeer/DuplicateFrom_Server_To_Client", false, 1)] static void DuplicateS2C () {
			var sourcePath = SearchEditor_CrossPeerFolder();
			var destPath = Search_CrossPeerFolder();
			
			CopyAllFilesWithoutMeta(new Component(sourcePath), new Component(destPath), false);
			EditorApplication.ExecuteMenuItem("Assets/Refresh");
		}
		
		[MenuItem ("XrossPeer/DuplicateFrom_Server_To_Client", true)] static bool IsComparedForServer () {
			return !Compare(false);
		}
		
		
		public class FileDatas {
			public readonly string hash;
			public readonly DateTime lastWriteTime;
			
			public FileDatas (string hash, DateTime lastWriteTime) {
				this.hash = hash;
				this.lastWriteTime = lastWriteTime;
			}
		}





		private static string Search_CrossPeerFolder () {
			var rootPath = Application.dataPath;

			var allCrossPeerRootPaths = Directory.GetDirectories(rootPath, "XrossPeer", SearchOption.AllDirectories)
				.Where(candidate => !candidate.Contains("Editor")).ToArray();
			foreach (var candidate in allCrossPeerRootPaths) {
				return candidate;
			}
			return string.Empty;
		}


		private static string SearchEditor_CrossPeerFolder () {
			var rootPath = Application.dataPath;

			var allEditorRootPaths = Directory.GetDirectories(rootPath, "Editor", SearchOption.AllDirectories);
			foreach (var editorRootPath in allEditorRootPaths) {
				var allCrossPeerFolderPaths = Directory.GetDirectories(editorRootPath, "XrossPeer");
				foreach (var candidate in allCrossPeerFolderPaths) {
					return candidate;
				}

			}
			return string.Empty;
		}




		/**
			copy server/client cross-peer files to server/client.
			そのうちあるPeerをどこにコピーするか、みたいなのを動かす感じになる。
		*/
		public static void CopyAllFilesWithoutMeta (Component fromBasePathSource, Component destinationBasePathSource, bool fromClientToServer) {
			var fromBasePath = fromBasePathSource.path;
			var destinationBasePath = destinationBasePathSource.path;

			if (!Directory.Exists(destinationBasePath)) {
				Debug.LogError("failed to find dest path:" + destinationBasePath);
				throw new Exception("failed to find dest path:" + destinationBasePath);
			}

			destinationBasePath = AddPathSplitterIfNeed(destinationBasePath.ToComponent());

			if (!Directory.Exists(fromBasePath)) {
				Debug.LogError("failed to find source path:" + fromBasePath);
				throw new Exception("failed to find source path:" + fromBasePath);
			}
			
			fromBasePath = AddPathSplitterIfNeed(fromBasePath.ToComponent());


			var allFilePaths = GetFilesRecursivePathInFolder(fromBasePath.ToComponent()).Where(path => !path.EndsWith(".meta")).ToList();
			
			foreach (var filePath in allFilePaths) {
				var fileName = Path.GetFileName(filePath);

				// ignore 
				if (fileName.StartsWith(".")) continue;


				var newFilePath = filePath.Replace(fromBasePath, destinationBasePath);
				
				var destinationModifiedPath = newFilePath;
				if (fromClientToServer) {
					destinationModifiedPath = destinationModifiedPath.Replace(".client.", ".server.");
				} else {
					destinationModifiedPath = destinationModifiedPath.Replace(".server.", ".client.");
				}

				Copy(filePath.ToComponent(), destinationModifiedPath.ToComponent());
			}
		}

		public static void Copy (Component sourceFilePath, Component destinationFilePath) {
			if ((File.GetAttributes(sourceFilePath.path) & FileAttributes.Directory) == FileAttributes.Directory) {
				Debug.LogError("Copy: should copy folderCopy for directory copy. sourceFilePath" + sourceFilePath);
				return;
			}
			
			// create directory if not exist.
			var destinationFileParentPath = Directory.GetParent(destinationFilePath.path).ToString();
			if (!Directory.Exists(destinationFileParentPath)) Directory.CreateDirectory(destinationFileParentPath);
			
			File.Copy(sourceFilePath.path, destinationFilePath.path, true);
		}


		public static List<string> GetFilesRecursivePathInFolder (Component path) {
			var list = new List<Component>();

			var filePaths = Directory.GetFiles(path.path).ToList();
			list.AddRange(filePaths.ToComponents());

			var folderPaths = Directory.GetDirectories(path.path);
			foreach (var folderPath in folderPaths) {
				GetFilesRecursivePathInFolderRecursive(folderPath.ToComponent(), list);
			}
			
			return list.Decomponents();
		}

		private static void GetFilesRecursivePathInFolderRecursive (Component path, List<Component> list) {
			var filePaths = Directory.GetFiles(path.path).ToList();
			list.AddRange(filePaths.ToComponents());

			var folderPaths = Directory.GetDirectories(path.path);
			foreach (var folderPath in folderPaths) {
				GetFilesRecursivePathInFolderRecursive(folderPath.ToComponent(), list);
			}
		}



		public static string AddPathSplitterIfNeed (Component path) {
			if (!path.path.EndsWith(XROSSPEER_PATH_DELIM)) {
				return path.path + XROSSPEER_PATH_DELIM;
			}

			return path.path;
		}
	}


	public static class ComponentExtension {

		public static Component ToComponent (this string path) {
			return new Component(path);
		}

		public static List<Component> ToComponents (this List<string> paths) {
			var components = new List<Component>();
			foreach (var path in paths) {
				components.Add(new Component(path));
			}
			return components;
		}

		public static List<string> Decomponents (this List<Component> components) {
			var paths = new List<string>();
			foreach (var component in components) {
				paths.Add(component.path);
			}
			return paths;
		}
	}

	public class Component {
		public Component (string path) {
			this.path = path;
		}

		private string pathData;
		public string path {
			get { return pathData; }
			set { pathData = value.Replace(XrossPeerDuplicator.XROSSPEER_NOT_PATH_DELIM, XrossPeerDuplicator.XROSSPEER_PATH_DELIM); }
		}

		public override string ToString() {
			return path;
		}
	}
}