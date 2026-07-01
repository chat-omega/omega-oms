using NuGet.Versioning;
using Velopack;

namespace ZeroPlus.Oms.Update
{
    public struct Information
    {
        public SemanticVersion Version { get; set; }
        public string ReleaseNotes { get; set; }
        public VelopackAsset UpdateInfo { get; set; }
        public UpdateManager UpdateManager { private get; init; }
        public readonly void ApplyUpdate(string[] args) => UpdateManager.ApplyUpdatesAndRestart(UpdateInfo, args);
    }
}
