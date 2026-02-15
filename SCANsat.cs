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
		private const string ScannerPartModuleTypeName = "SCANsat.SCAN_PartModules.SCANsat";
		private const int KnownScanMask = (1 << 9) - 1;

		private static bool typesInitialized;
		private static Type controllerType;
		private static Type utilType;
		private static Type scannerPartModuleType;

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

		private static FieldInfo scannerSensorTypeField;
		private static PropertyInfo scannerScanningNowProperty;
		private static MethodInfo scannerStartScanMethod;
		private static MethodInfo scannerStopScanMethod;
		private static bool legacyDeprecationWarned;
		[ThreadStatic] private static int suppressLegacyWarningDepth;

		internal static IDisposable SuppressLegacyServiceWarning() {
			suppressLegacyWarningDepth++;
			return new LegacyWarningScope();
		}

		private sealed class LegacyWarningScope : IDisposable {
			private bool disposed;

			public void Dispose() {
				if(disposed)
					return;
				disposed = true;
				if(suppressLegacyWarningDepth > 0)
					suppressLegacyWarningDepth--;
			}
		}

		private static void WarnLegacyServiceUsage() {
			if(legacyDeprecationWarned || suppressLegacyWarningDepth > 0)
				return;

			legacyDeprecationWarned = true;
			Logger.Warning("Service 'SCANsat' is deprecated and will be removed in a future major release. Use service 'Scansat' (Python: conn.scansat) instead.");
		}

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
				if(scannerPartModuleType == null)
					scannerPartModuleType = assembly.GetType(ScannerPartModuleTypeName, false);
				if(controllerType != null && utilType != null && scannerPartModuleType != null)
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

			if(scannerPartModuleType != null) {
				scannerSensorTypeField = scannerPartModuleType.GetField("sensorType", BindingFlags.Public | BindingFlags.Instance);
				scannerScanningNowProperty = scannerPartModuleType.GetProperty("scanningNow", BindingFlags.Public | BindingFlags.Instance);
				scannerStartScanMethod = scannerPartModuleType.GetMethod("startScan", BindingFlags.Public | BindingFlags.Instance);
				scannerStopScanMethod = scannerPartModuleType.GetMethod("stopScan", BindingFlags.Public | BindingFlags.Instance);
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

		private static Vessel FindVessel(string vesselName) {
			if(string.IsNullOrWhiteSpace(vesselName))
				return FlightGlobals.ActiveVessel;

			if(FlightGlobals.VesselsLoaded == null)
				return null;

			return FlightGlobals.VesselsLoaded.FirstOrDefault(v =>
				string.Equals(v.vesselName, vesselName, StringComparison.OrdinalIgnoreCase));
		}

		private static Vessel RequireVessel(string vesselName) {
			Vessel vessel = FindVessel(vesselName);
			if(vessel == null)
				throw new SCANsatServiceException("Vessel not found in loaded flight scene: " + (vesselName ?? "<active vessel>"));
			return vessel;
		}

		private static int GetSensorMask(object scannerModule) {
			if(scannerSensorTypeField == null)
				return 0;
			return Convert.ToInt32(scannerSensorTypeField.GetValue(scannerModule));
		}

		private static bool IsScannerActive(object scannerModule) {
			if(scannerScanningNowProperty == null)
				return false;
			return (bool)scannerScanningNowProperty.GetValue(scannerModule, null);
		}

		private static IEnumerable<PartModule> GetScannerModules(Vessel vessel) {
			if(vessel == null || scannerPartModuleType == null || vessel.parts == null)
				yield break;

			foreach(Part part in vessel.parts) {
				if(part == null || part.Modules == null)
					continue;
				foreach(PartModule module in part.Modules) {
					if(module != null && scannerPartModuleType.IsInstanceOfType(module))
						yield return module;
				}
			}
		}

		private static IEnumerable<ScannerFamily> EnumerateScannerFamilies(int sensorMask) {
			foreach(ScannerFamily family in new[] {
				ScannerFamily.AltimetryLoRes,
				ScannerFamily.AltimetryHiRes,
				ScannerFamily.VisualLoRes,
				ScannerFamily.Biome,
				ScannerFamily.Anomaly,
				ScannerFamily.AnomalyDetail,
				ScannerFamily.VisualHiRes,
				ScannerFamily.ResourceLoRes,
				ScannerFamily.ResourceHiRes
			}) {
				int familyMask = (int)family;
				if((sensorMask & familyMask) != 0)
					yield return family;
			}
		}

		private static IEnumerable<ScannerModuleStatus> DescribeScannerModules(Vessel vessel, ScannerFamily filterFamily = ScannerFamily.Nothing, bool activeOnly = false) {
			int filterMask = NormalizeFamilyMask(filterFamily);
			foreach(PartModule module in GetScannerModules(vessel)) {
				int sensorMask = GetSensorMask(module);
				bool isActive = IsScannerActive(module);
				if(activeOnly && !isActive)
					continue;
				if(filterMask != 0 && (sensorMask & filterMask) == 0)
					continue;

				string partTitle = module.part != null && module.part.partInfo != null ? module.part.partInfo.title : module.part.partName;
				IList<ScannerFamily> families = EnumerateScannerFamilies(sensorMask).ToList();
				if(families.Count == 0) {
					yield return new ScannerModuleStatus(vessel.vesselName, module.part.flightID, partTitle, ScannerFamily.Nothing, sensorMask, isActive);
					continue;
				}

				foreach(ScannerFamily family in families)
					yield return new ScannerModuleStatus(vessel.vesselName, module.part.flightID, partTitle, family, sensorMask, isActive);
			}
		}

		private static int NormalizeFamilyMask(ScannerFamily family) {
			if(family == ScannerFamily.SAR)
				return (int)ScannerFamily.AltimetryHiRes;

			return ((int)family) & KnownScanMask;
		}

		private static PartModule FindMatchingScannerModule(Vessel vessel, uint partFlightId, int familyMask) {
			return GetScannerModules(vessel)
				.FirstOrDefault(module =>
					module.part != null &&
					module.part.flightID == partFlightId &&
					(GetSensorMask(module) & familyMask) != 0);
		}

		private static void RequireMethods(params MethodInfo[] methods) {
			if(methods.Any(m => m == null))
				throw new SCANsatServiceException("SCANsat API methods are unavailable for this SCANsat build.");
		}

		private static void RequireScannerMethods() {
			InitTypes();
			if(scannerPartModuleType == null || scannerSensorTypeField == null || scannerScanningNowProperty == null || scannerStartScanMethod == null || scannerStopScanMethod == null)
				throw new SCANsatServiceException("SCANsat scanner part-module API is unavailable for this SCANsat build.");
		}

		private static void SetScannerState(PartModule scannerModule, bool enabled) {
			if(scannerModule == null)
				throw new SCANsatServiceException("Scanner module not found.");

			bool currentlyActive = IsScannerActive(scannerModule);
			if(enabled == currentlyActive)
				return;

			MethodInfo method = enabled ? scannerStartScanMethod : scannerStopScanMethod;
			method.Invoke(scannerModule, null);
		}

		/// <summary>
		/// A value indicating whether the SCANsat service is available.
		/// </summary>
		[KRPCProperty]
		public static bool APIReady {
			get {
				WarnLegacyServiceUsage();
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
		public static bool ScannerAPIReady {
			get {
				WarnLegacyServiceUsage();
				InitTypes();
				return scannerPartModuleType != null &&
					scannerSensorTypeField != null &&
					scannerScanningNowProperty != null &&
					scannerStartScanMethod != null &&
					scannerStopScanMethod != null;
			}
		}

		[KRPCProperty]
		public static int ActiveSensors {
			get {
				WarnLegacyServiceUsage();
				if(!APIReady || activeSensorsProperty == null)
					return -1;
				return (int)activeSensorsProperty.GetValue(ControllerInstance, null);
			}
		}

		[KRPCProperty]
		public static int ActiveVessels {
			get {
				WarnLegacyServiceUsage();
				if(!APIReady || activeVesselsProperty == null)
					return -1;
				return (int)activeVesselsProperty.GetValue(ControllerInstance, null);
			}
		}

		[KRPCProperty]
		public static int ActualPasses {
			get {
				WarnLegacyServiceUsage();
				if(!APIReady || actualPassesProperty == null)
					return -1;
				return (int)actualPassesProperty.GetValue(ControllerInstance, null);
			}
		}

		[KRPCProcedure]
		public static bool BodyKnown(string bodyName) {
			WarnLegacyServiceUsage();
			if(!APIReady || getDataMethod == null)
				return false;
			return getDataMethod.Invoke(ControllerInstance, new object[] { bodyName }) != null;
		}

		[KRPCProcedure]
		public static double Coverage(string bodyName, ScanType scanType) {
			WarnLegacyServiceUsage();
			RequireMethods(coverageMethod);
			CelestialBody body = RequireBody(bodyName);
			object result = coverageMethod.Invoke(null, new object[] { (int)scanType, body });
			return Convert.ToDouble(result);
		}

		[KRPCProcedure]
		public static double CoverageBySensor(string bodyName, ScannerFamily family) {
			WarnLegacyServiceUsage();
			int mask = NormalizeFamilyMask(family);
			if(mask == 0)
				throw new SCANsatServiceException("Scanner family is required.");
			return Coverage(bodyName, (ScanType)mask);
		}

		[KRPCProcedure]
		public static bool IsCovered(string bodyName, double latitude, double longitude, ScanType scanType) {
			WarnLegacyServiceUsage();
			RequireMethods(isCoveredMethod);
			CelestialBody body = RequireBody(bodyName);
			object scanTypeArg = isCoveredMethod.GetParameters()[3].ParameterType == typeof(short) ? (object)(short)(int)scanType : (int)scanType;
			object result = isCoveredMethod.Invoke(null, new object[] { longitude, latitude, body, scanTypeArg });
			return result != null && (bool)result;
		}

		[KRPCProcedure]
		public static double ResourceValue(string bodyName, double latitude, double longitude, string resourceName, bool biomeLock = false) {
			WarnLegacyServiceUsage();
			RequireMethods(resourceOverlayMethod);
			if(string.IsNullOrWhiteSpace(resourceName))
				throw new SCANsatServiceException("Resource name is required.");

			CelestialBody body = RequireBody(bodyName);
			object result = resourceOverlayMethod.Invoke(null, new object[] { latitude, longitude, resourceName, body, biomeLock });
			return Convert.ToDouble(result);
		}

		[KRPCProcedure]
		public static double Elevation(string bodyName, double latitude, double longitude) {
			WarnLegacyServiceUsage();
			RequireMethods(getElevationMethod);
			CelestialBody body = RequireBody(bodyName);
			object result = getElevationMethod.Invoke(null, new object[] { body, longitude, latitude });
			return Convert.ToDouble(result);
		}

		[KRPCProcedure]
		public static double Slope(string bodyName, double latitude, double longitude, double sampleOffsetMeters = 5.0) {
			WarnLegacyServiceUsage();
			RequireMethods(getElevationMethod, slopeMethod);
			CelestialBody body = RequireBody(bodyName);
			double centerElevation = Elevation(bodyName, latitude, longitude);
			object result = slopeMethod.Invoke(null, new object[] { centerElevation, body, longitude, latitude, sampleOffsetMeters });
			return Convert.ToDouble(result);
		}

		[KRPCProcedure]
		public static IList<string> AvailableResources() {
			WarnLegacyServiceUsage();
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

		[KRPCProcedure]
		public static IList<ScannerModuleStatus> GetScanners(string vesselName = null, ScannerFamily family = ScannerFamily.Nothing) {
			WarnLegacyServiceUsage();
			RequireScannerMethods();
			Vessel vessel = RequireVessel(vesselName);
			return DescribeScannerModules(vessel, family, false).ToList();
		}

		[KRPCProcedure]
		public static IList<ScannerModuleStatus> GetActiveScanners(string vesselName = null, ScannerFamily family = ScannerFamily.Nothing) {
			WarnLegacyServiceUsage();
			RequireScannerMethods();
			Vessel vessel = RequireVessel(vesselName);
			return DescribeScannerModules(vessel, family, true).ToList();
		}

		[KRPCProcedure]
		public static bool IsScannerEnabled(string vesselName, uint partFlightId, ScannerFamily family) {
			WarnLegacyServiceUsage();
			RequireScannerMethods();
			Vessel vessel = RequireVessel(vesselName);
			int familyMask = NormalizeFamilyMask(family);
			if(familyMask == 0)
				throw new SCANsatServiceException("Scanner family is required.");

			PartModule scannerModule = FindMatchingScannerModule(vessel, partFlightId, familyMask);
			if(scannerModule == null)
				throw new SCANsatServiceException("Scanner module not found on vessel '" + vessel.vesselName + "' for part " + partFlightId + " and family " + family + ".");

			return IsScannerActive(scannerModule);
		}

		[KRPCProcedure]
		public static void SetScannerEnabled(string vesselName, uint partFlightId, ScannerFamily family, bool enabled) {
			WarnLegacyServiceUsage();
			RequireScannerMethods();
			Vessel vessel = RequireVessel(vesselName);
			int familyMask = NormalizeFamilyMask(family);
			if(familyMask == 0)
				throw new SCANsatServiceException("Scanner family is required.");

			PartModule scannerModule = FindMatchingScannerModule(vessel, partFlightId, familyMask);
			if(scannerModule == null)
				throw new SCANsatServiceException("Scanner module not found on vessel '" + vessel.vesselName + "' for part " + partFlightId + " and family " + family + ".");

			SetScannerState(scannerModule, enabled);
		}

		[KRPCProcedure]
		public static uint SetSingleScannerEnabled(string vesselName, ScannerFamily family, uint preferredPartFlightId = 0) {
			WarnLegacyServiceUsage();
			RequireScannerMethods();
			Vessel vessel = RequireVessel(vesselName);
			int familyMask = NormalizeFamilyMask(family);
			if(familyMask == 0)
				throw new SCANsatServiceException("Scanner family is required.");

			List<PartModule> matchingModules = GetScannerModules(vessel)
				.Where(module => (GetSensorMask(module) & familyMask) != 0)
				.ToList();
			if(matchingModules.Count == 0)
				throw new SCANsatServiceException("No scanner modules found on vessel '" + vessel.vesselName + "' for family " + family + ".");

			PartModule selected = null;
			if(preferredPartFlightId != 0)
				selected = matchingModules.FirstOrDefault(module => module.part != null && module.part.flightID == preferredPartFlightId);
			if(selected == null)
				selected = matchingModules.FirstOrDefault(module => IsScannerActive(module)) ?? matchingModules[0];

			foreach(PartModule module in matchingModules) {
				bool enable = module == selected;
				SetScannerState(module, enable);
			}

			return selected.part.flightID;
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

	[KRPCEnum(Service = "SCANsat")]
	public enum ScannerFamily {
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
		SAR = AltimetryHiRes
	}

	[KRPCClass(Service = "SCANsat")]
	public class ScannerModuleStatus {
		public ScannerModuleStatus(string vesselName, uint partFlightId, string partTitle, ScannerFamily family, int sensorMask, bool active) {
			VesselName = vesselName;
			PartFlightId = partFlightId;
			PartTitle = partTitle;
			Family = family;
			SensorMask = sensorMask;
			Active = active;
		}

		[KRPCProperty]
		public string VesselName { get; private set; }

		[KRPCProperty]
		public uint PartFlightId { get; private set; }

		[KRPCProperty]
		public string PartTitle { get; private set; }

		[KRPCProperty]
		public ScannerFamily Family { get; private set; }

		[KRPCProperty]
		public int SensorMask { get; private set; }

		[KRPCProperty]
		public bool Active { get; private set; }
	}

	[KRPCException(Service = "SCANsat")]
	public class SCANsatServiceException : Exception {
		public SCANsatServiceException(string message) : base(message) { }
	}
}
