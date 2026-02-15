using System;
using System.Collections.Generic;
using System.Linq;

using KRPC.Service.Attributes;

namespace KRPC.SCANsat {
	/// <summary>
	/// Compatibility rollout service alias for SCANsat.
	/// New clients should prefer this service name for ergonomic bindings (e.g. conn.scansat in Python).
	/// Legacy SCANsat service remains available for backward compatibility.
	/// </summary>
	[KRPCService(Name = "Scansat", GameScene = Service.GameScene.Flight)]
	public static class Scansat {
		private static T Legacyless<T>(Func<T> func) {
			using(SCANsat.SuppressLegacyServiceWarning())
				return func();
		}

		private static void Legacyless(Action action) {
			using(SCANsat.SuppressLegacyServiceWarning())
				action();
		}

		[KRPCProperty]
		public static bool APIReady => Legacyless(() => SCANsat.APIReady);

		[KRPCProperty]
		public static bool ScannerAPIReady => Legacyless(() => SCANsat.ScannerAPIReady);

		[KRPCProperty]
		public static int ActiveSensors => Legacyless(() => SCANsat.ActiveSensors);

		[KRPCProperty]
		public static int ActiveVessels => Legacyless(() => SCANsat.ActiveVessels);

		[KRPCProperty]
		public static int ActualPasses => Legacyless(() => SCANsat.ActualPasses);

		[KRPCProcedure]
		public static bool BodyKnown(string bodyName) => Legacyless(() => SCANsat.BodyKnown(bodyName));

		[KRPCProcedure]
		public static double Coverage(string bodyName, ScanType scanType) => Legacyless(() => SCANsat.Coverage(bodyName, ToLegacyScanType(scanType)));

		[KRPCProcedure]
		public static double CoverageBySensor(string bodyName, ScannerFamily family) => Legacyless(() => SCANsat.CoverageBySensor(bodyName, ToLegacyScannerFamily(family)));

		[KRPCProcedure]
		public static bool IsCovered(string bodyName, double latitude, double longitude, ScanType scanType) =>
			Legacyless(() => SCANsat.IsCovered(bodyName, latitude, longitude, ToLegacyScanType(scanType)));

		[KRPCProcedure]
		public static double ResourceValue(string bodyName, double latitude, double longitude, string resourceName, bool biomeLock = false) =>
			Legacyless(() => SCANsat.ResourceValue(bodyName, latitude, longitude, resourceName, biomeLock));

		[KRPCProcedure]
		public static double Elevation(string bodyName, double latitude, double longitude) =>
			Legacyless(() => SCANsat.Elevation(bodyName, latitude, longitude));

		[KRPCProcedure]
		public static double Slope(string bodyName, double latitude, double longitude, double sampleOffsetMeters = 5.0) =>
			Legacyless(() => SCANsat.Slope(bodyName, latitude, longitude, sampleOffsetMeters));

		[KRPCProcedure]
		public static IList<string> AvailableResources() => Legacyless(() => SCANsat.AvailableResources());

		[KRPCProcedure]
		public static IList<ScannerModuleStatus> GetScanners(string vesselName = null, ScannerFamily family = ScannerFamily.Nothing) =>
			Legacyless(() => SCANsat.GetScanners(vesselName, ToLegacyScannerFamily(family)))
				.Select(FromLegacyScannerModuleStatus)
				.ToList();

		[KRPCProcedure]
		public static IList<ScannerModuleStatus> GetActiveScanners(string vesselName = null, ScannerFamily family = ScannerFamily.Nothing) =>
			Legacyless(() => SCANsat.GetActiveScanners(vesselName, ToLegacyScannerFamily(family)))
				.Select(FromLegacyScannerModuleStatus)
				.ToList();

		[KRPCProcedure]
		public static bool IsScannerEnabled(string vesselName, uint partFlightId, ScannerFamily family) =>
			Legacyless(() => SCANsat.IsScannerEnabled(vesselName, partFlightId, ToLegacyScannerFamily(family)));

		[KRPCProcedure]
		public static void SetScannerEnabled(string vesselName, uint partFlightId, ScannerFamily family, bool enabled) {
			Legacyless(() => SCANsat.SetScannerEnabled(vesselName, partFlightId, ToLegacyScannerFamily(family), enabled));
		}

		[KRPCProcedure]
		public static uint SetSingleScannerEnabled(string vesselName, ScannerFamily family, uint preferredPartFlightId = 0) =>
			Legacyless(() => SCANsat.SetSingleScannerEnabled(vesselName, ToLegacyScannerFamily(family), preferredPartFlightId));

		private static KRPC.SCANsat.ScanType ToLegacyScanType(ScanType scanType) => (KRPC.SCANsat.ScanType)(int)scanType;

		private static KRPC.SCANsat.ScannerFamily ToLegacyScannerFamily(ScannerFamily scannerFamily) => (KRPC.SCANsat.ScannerFamily)(int)scannerFamily;

		private static ScannerFamily FromLegacyScannerFamily(KRPC.SCANsat.ScannerFamily scannerFamily) => (ScannerFamily)(int)scannerFamily;

		private static ScannerModuleStatus FromLegacyScannerModuleStatus(KRPC.SCANsat.ScannerModuleStatus status) =>
			new ScannerModuleStatus(status.VesselName, status.PartFlightId, status.PartTitle, FromLegacyScannerFamily(status.Family), status.SensorMask, status.Active);

		[KRPCEnum]
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

		[KRPCEnum]
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

		[KRPCClass]
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

		[KRPCException(MappedException = typeof(SCANsatServiceException))]
		public class ScansatServiceException : Exception {
			public ScansatServiceException(string message) : base(message) { }
		}
	}
}
