using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed class StreetProxyDoorSnapshot
    {
        public string Address { get; set; } = "";
        public string Size { get; set; } = "";
        public int BuildingVersion { get; set; }
        public int Frame { get; set; }
        public int LayoutInstance { get; set; }
        public StreetFrame Layout { get; set; }
        public string Refusal { get; set; } = "";
        public bool Complete { get; set; }
        public bool MultipleHeights { get; set; }
        public bool ViewConfigured { get; set; }
        public bool GuardDetected { get; set; }
        public string GuardReason { get; set; } = "";
        public double? FloorHeight { get; set; }
        public string Eligibility { get; set; } = "";
        public StreetDoorFrame[] Exterior { get; set; } = Array.Empty<StreetDoorFrame>();
        public StreetDoorFrame[] Interior { get; set; } = Array.Empty<StreetDoorFrame>();
        public StreetEdgePair[] SuppliedEdges { get; set; } = Array.Empty<StreetEdgePair>();
        public StreetDoorDecision? Decision { get; set; }
        public string FrameQualification => "Exit-zone roots and exterior markers are candidate frames, not measured jambs; supplied edges require independent provenance.";
        public bool RendererAvailable => false;
    }

    public sealed class StreetProxyRendererMetadata
    {
        public int Instance { get; set; }
        public string Name { get; set; } = "";
        public string Scene { get; set; } = "";
        public string Kind { get; set; } = "";
        public Position Minimum { get; set; }
        public Position Maximum { get; set; }
        public StreetRadiusClass Radius30 { get; set; }
        public StreetRadiusClass Radius60 { get; set; }
        public StreetRadiusClass Radius120 { get; set; }
        public bool Enabled { get; set; }
        public bool Active { get; set; }
        public bool MarkedStatic { get; set; }
        public bool CandidateStaticMesh { get; set; }
        public string Exclusion { get; set; } = "";
        public string Mesh { get; set; } = "";
        public int Submeshes { get; set; }
        public string[] Materials { get; set; } = Array.Empty<string>();
        public string[] Shaders { get; set; } = Array.Empty<string>();
        public bool HasPropertyBlock { get; set; }
        public string Shadows { get; set; } = "";
        public string[] OwnerHints { get; set; } = Array.Empty<string>();
        public string[] OwnerMembership { get; set; } = Array.Empty<string>();
        public string Ownership => "Observed loaded-scene LODGroup, ViewBlockingEntity fade/hide and CityBuildingController base-LOD references plus ancestry hints; other owners, property-block values and shader behavior unproven.";
    }

    public sealed class StreetProxyBodyMetadata
    {
        public int Instance { get; set; }
        public string Resident { get; set; } = "";
        public string Kind { get; set; } = "";
        public Position Position { get; set; }
        public double Speed { get; set; }
        public bool Active { get; set; }
    }

    public sealed class StreetProxyEnvironmentMetadata
    {
        public int Instance { get; set; }
        public string Kind { get; set; } = "";
        public bool Enabled { get; set; }
        public bool Active { get; set; }
        public Position Position { get; set; }
        public double Range { get; set; }
        public double IntensityOrWeight { get; set; }
        public string Detail { get; set; } = "";
    }

    public sealed class StreetProxyInventorySnapshot
    {
        public int Frame { get; set; }
        public string Address { get; set; } = "";
        public Position Anchor { get; set; }
        public bool Complete { get; set; }
        public string Refusal { get; set; } = "";
        public int NodesVisited { get; set; }
        public int Rows { get; set; }
        public string Ambient { get; set; } = "";
        public double[] MainCameraView { get; set; } = Array.Empty<double>();
        public double[] MainCameraProjection { get; set; } = Array.Empty<double>();
        public List<StreetProxyRendererMetadata> Renderers { get; } = new List<StreetProxyRendererMetadata>();
        public List<StreetProxyBodyMetadata> Bodies { get; } = new List<StreetProxyBodyMetadata>();
        public List<StreetProxyEnvironmentMetadata> Environment { get; } = new List<StreetProxyEnvironmentMetadata>();
        // Index order: 30,60,120 m. Velocity snapshot (>0.01 m/s), not observed displacement over time.
        public int[] ActiveResidents { get; } = new int[3];
        public int[] MovingResidents { get; } = new int[3];
        public int[] ActiveOwnedVehicles { get; } = new int[3];
        public int[] MovingOwnedVehicles { get; } = new int[3];
        public int[] ActiveNativeTraffic { get; } = new int[3];
        public int[] MovingNativeTraffic { get; } = new int[3];
        public bool CountsComplete => Complete;
        public string Scope => "Loaded SceneManager scenes (including inactive objects), plus existing owned bodies; persistent/unloaded scenes and assets are not enumerated.";
        public bool StaticMembershipProven => false;
        public bool RendererAvailable => false;
    }
}
