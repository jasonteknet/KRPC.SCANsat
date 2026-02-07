using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using KRPC.Service.Attributes;

namespace KRPC.SCANsat {
	[KRPCService(GameScene = Service.GameScene.Flight)]
	public static class SCANsat {
		private const string ControllerTypeName = "SCANsat.SCANcontroller";
		private static readonly string[] UtilTypeNames = { "SCANsat.SCANUtil", "SCANsat.SCANutil" };

		private static bool typesInitialized;
		private static Type controllerType;
		private static Type utilType;

		private static FieldInfo controllerField;
		private static PropertyInfo controllerProperty;
		private static MethodInfo getDataMethod;
		private static PropertyInfo activeSensorsProperty;
		private static PropertyInfo activeVesselsProperty;
		private static PropertyInfo actualPassesProperty;

		private static MethodInfo resourcesMethod;
		private static PropertyInfo resourceNameProperty;

		private static MethodInfo coverageMethod;
		private static MethodInfo isCoveredMethod;
		private static MethodInfo resourceOverlayMethod;
		private static MethodInfo getElevationMethod;
		private static MethodInfo slopeMethod;

		private static void InitTypes() {
			if(typesInitialized)
				return;

			foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				if(controllerType == null)
					controllerType = assembly.GetType(ControllerTypeName, false);
				if(utilType == null) {
					foreach(string utilTypeName in UtilTypeNames) {
						utilType = assembly.GetType(utilTypeName, false);
						if(utilType != null)
							break;
					}
				}
				if(controllerType != null && utilType != null)
					break;
			}

			if(controllerType != null) {
				controllerField = controllerType.GetField("controller", BindingFlags.Public | BindingFlags.Static);
				if(controllerField == null)
					controllerProperty = controllerType.GetProperty("controller", BindingFlags.Public | BindingFlags.Static);
				getDataMethod = controllerType.GetMethod("getData", new Type[] { typeof(string) });
				activeSensorsProperty = controllerType.GetProperty("ActiveSensors", BindingFlags.Public | BindingFlags.Instance);
				activeVesselsProperty = controllerType.GetProperty("ActiveVessels", BindingFlags.Public | BindingFlags.Instance);
				actualPassesProperty = controllerType.GetProperty("ActualPasses", BindingFlags.Public | BindingFlags.Instance);
				resourcesMethod = controllerType.GetMethod("resources", BindingFlags.Public | BindingFlags.Static);
			}

			if(utilType != null) {
				coverageMethod = utilType.GetMethod("GetCoverage", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(CelestialBody) }, null);
				isCoveredMethod = utilType.GetMethod("isCovered", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(double), typeof(double), typeof(CelestialBody), typeof(int) }, null);
				if(isCoveredMethod == null)
					isCoveredMethod = utilType.GetMethod("isCoveredShort", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(double), typeof(double), typeof(CelestialBody), typeof(short) }, null);
				resourceOverlayMethod = utilType.GetMethod("ResourceOverlay", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(double), typeof(double), typeof(string), typeof(CelestialBody), typeof(bool) }, null);
				getElevationMethod = utilType.GetMethod("getElevation", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(CelestialBody), typeof(double), typeof(double) }, null);
				slopeMethod = utilType.GetMethod("slope", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(double), typeof(CelestialBody), typeof(double), typeof(double), typeof(double) }, null);
			}

			typesInitialized = true;
		}

		private static object ControllerInstance {
			get {
				InitTypes();
				if(controllerField != null)
					return controllerField.GetValue(null);
				if(controllerProperty != null)
					return controllerProperty.GetValue(null, null);
				return null;
			}
		}

		private static CelestialBody FindBody(string bodyName) {
			if(string.IsNullOrWhiteSpace(bodyName) || FlightGlobals.Bodies == null)
				return null;

			return FlightGlobals.Bodies.FirstOrDefault(b =>
				string.Equals(b.bodyName, bodyName, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(b.displayName, bodyName, StringComparison.OrdinalIgnoreCase));
		}

		private static CelestialBody RequireBody(string bodyName) {
			CelestialBody body = FindBody(bodyName);
			if(body == null)
				throw new SCANsatServiceException("Celestial body not found: " + bodyName);
			return body;
		}

		private static void RequireMethods(params MethodInfo[] methods) {
			if(methods.Any(m => m == null))
				throw new SCANsatServiceException("SCANsat API methods are unavailable for this SCANsat build.");
		}

		/// <summary>
		/// A value indicating whether the SCANsat service is available.
		/// </summary>
		[KRPCProperty]
		public static bool APIReady {
			get {
				InitTypes();
				bool utilReady = utilType != null &&
					coverageMethod != null &&
					isCoveredMethod != null &&
					resourceOverlayMethod != null &&
					getElevationMethod != null &&
					slopeMethod != null;
				return utilReady;
			}
		}

		[KRPCProperty]
		public static int ActiveSensors {
			get {
				if(!APIReady || activeSensorsProperty == null)
					return -1;
				return (int)activeSensorsProperty.GetValue(ControllerInstance, null);
			}
		}

		[KRPCProperty]
		public static int ActiveVessels {
			get {
				if(!APIReady || activeVesselsProperty == null)
					return -1;
				return (int)activeVesselsProperty.GetValue(ControllerInstance, null);
			}
		}

		[KRPCProperty]
		public static int ActualPasses {
			get {
				if(!APIReady || actualPassesProperty == null)
					return -1;
				return (int)actualPassesProperty.GetValue(ControllerInstance, null);
			}
		}

		[KRPCProcedure]
		public static bool BodyKnown(string bodyName) {
			if(!APIReady || getDataMethod == null)
				return false;
			return getDataMethod.Invoke(ControllerInstance, new object[] { bodyName }) != null;
		}

		[KRPCProcedure]
		public static double Coverage(string bodyName, ScanType scanType) {
			RequireMethods(coverageMethod);
			CelestialBody body = RequireBody(bodyName);
			object result = coverageMethod.Invoke(null, new object[] { (int)scanType, body });
			return Convert.ToDouble(result);
		}

		[KRPCProcedure]
		public static bool IsCovered(string bodyName, double latitude, double longitude, ScanType scanType) {
			RequireMethods(isCoveredMethod);
			CelestialBody body = RequireBody(bodyName);
			object scanTypeArg = isCoveredMethod.GetParameters()[3].ParameterType == typeof(short) ? (object)(short)(int)scanType : (int)scanType;
			object result = isCoveredMethod.Invoke(null, new object[] { longitude, latitude, body, scanTypeArg });
			return result != null && (bool)result;
		}

		[KRPCProcedure]
		public static double ResourceValue(string bodyName, double latitude, double longitude, string resourceName, bool biomeLock = false) {
			RequireMethods(resourceOverlayMethod);
			if(string.IsNullOrWhiteSpace(resourceName))
				throw new SCANsatServiceException("Resource name is required.");

			CelestialBody body = RequireBody(bodyName);
			object result = resourceOverlayMethod.Invoke(null, new object[] { latitude, longitude, resourceName, body, biomeLock });
			return Convert.ToDouble(result);
		}

		[KRPCProcedure]
		public static double Elevation(string bodyName, double latitude, double longitude) {
			RequireMethods(getElevationMethod);
			CelestialBody body = RequireBody(bodyName);
			object result = getElevationMethod.Invoke(null, new object[] { body, longitude, latitude });
			return Convert.ToDouble(result);
		}

		[KRPCProcedure]
		public static double Slope(string bodyName, double latitude, double longitude, double sampleOffsetMeters = 5.0) {
			RequireMethods(getElevationMethod, slopeMethod);
			CelestialBody body = RequireBody(bodyName);
			double centerElevation = Elevation(bodyName, latitude, longitude);
			object result = slopeMethod.Invoke(null, new object[] { centerElevation, body, longitude, latitude, sampleOffsetMeters });
			return Convert.ToDouble(result);
		}

		[KRPCProcedure]
		public static IList<string> AvailableResources() {
			if(!APIReady || resourcesMethod == null)
				return new List<string>();

			IEnumerable resources = resourcesMethod.Invoke(null, null) as IEnumerable;
			if(resources == null)
				return new List<string>();

			List<string> names = new List<string>();
			foreach(object resource in resources) {
				if(resource == null)
					continue;
				if(resourceNameProperty == null)
					resourceNameProperty = resource.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
				if(resourceNameProperty == null)
					continue;
				string name = resourceNameProperty.GetValue(resource, null) as string;
				if(!string.IsNullOrEmpty(name))
					names.Add(name);
			}
			return names;
		}
	}

	[KRPCEnum(Service = "SCANsat")]
	public enum ScanType {
		Nothing = 0,
		AltimetryLoRes = 1 << 0,
		AltimetryHiRes = 1 << 1,
		Altimetry = (1 << 2) - 1,
		VisualLoRes = 1 << 2,
		Biome = 1 << 3,
		Anomaly = 1 << 4,
		AnomalyDetail = 1 << 5,
		VisualHiRes = 1 << 6,
		ResourceLoRes = 1 << 7,
		ResourceHiRes = 1 << 8,
		EverythingSCAN = (1 << 9) - 1,
		Science = 143,
		Everything = int.MaxValue
	}

	[KRPCException(Service = "SCANsat")]
	public class SCANsatServiceException : Exception {
		public SCANsatServiceException(string message) : base(message) { }
	}
}
