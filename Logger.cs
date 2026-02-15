namespace KRPC.SCANsat {
	internal static class Logger {
		private static readonly UnityEngine.ILogger UnityLogger = UnityEngine.Debug.unityLogger;

		internal static void Debug(string message) {
			UnityLogger.Log(UnityEngine.LogType.Log, "[KRPC.SCANsat] " + message);
		}

		internal static void Error(string message) {
			UnityLogger.Log(UnityEngine.LogType.Error, "[KRPC.SCANsat] " + message);
		}

		internal static void Warning(string message) {
			UnityLogger.Log(UnityEngine.LogType.Warning, "[KRPC.SCANsat] " + message);
		}
	}
}
